using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCpanmDockerfileTests
{
    [Fact]
    public void BuildCpanmDockerfile_UsesSingleStage()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        Assert.Single(builder.Stages);
    }

    [Fact]
    public void BuildCpanmDockerfile_ProducesExpectedStatementSequence()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileRunStatement",        // install cpanm
            "DockerfileCopyStatement",       // cpanfile (dependency manifest)
            "DockerfileRunStatement",        // cpanm --installdeps
            "DockerfileCopyStatement",       // application source
            "DockerfileEntrypointStatement",
        ];

        var actual = builder.Stages[0].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.Equal(expected, actual);
    }

}

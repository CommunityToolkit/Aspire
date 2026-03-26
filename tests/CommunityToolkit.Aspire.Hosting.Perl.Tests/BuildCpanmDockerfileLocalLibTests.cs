using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCpanmDockerfileLocalLibTests
{
    [Fact]
    public void BuildCpanmDockerfile_WithLocalLib_ProducesExpectedStatementSequence()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            localLibPath: "local");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileRunStatement",        // install cpanm
            "DockerfileCopyStatement",       // cpanfile
            "DockerfileRunStatement",        // cpanm --local-lib --installdeps
            "DockerfileCopyStatement",       // application source
            "DockerfileEnvStatement",        // PERL5LIB
            "DockerfileEnvStatement",        // PERL_LOCAL_LIB_ROOT
            "DockerfileEntrypointStatement",
        ];

        var actual = builder.Stages[0].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildCpanmDockerfile_WithoutLocalLib_HasNoEnvStatements()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCpanmDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            localLibPath: null);
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        var actual = builder.Stages[0].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.DoesNotContain("DockerfileEnvStatement", actual);
    }

}

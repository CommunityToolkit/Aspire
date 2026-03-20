using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileLocalLibTests
{
    [Fact]
    public void BuildCartonDockerfile_WithLocalLib_RuntimeStageProducesExpectedStatementSequence()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            localLibPath: "local");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        string[] expected =
        [
            "DockerfileFromStatement",
            "DockerfileWorkDirStatement",
            "DockerfileCopyFromStatement",   // copy from build stage
            "DockerfileEnvStatement",        // PERL5LIB
            "DockerfileEnvStatement",        // PERL_LOCAL_LIB_ROOT
            "DockerfileEntrypointStatement",
        ];

        var actual = builder.Stages[1].Statements
            .Select(s => s.GetType().Name)
            .ToArray();

        Assert.Equal(expected, actual);
    }

}

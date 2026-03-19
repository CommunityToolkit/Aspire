using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileLocalLibTests
{
    [Fact]
    public void BuildCartonDockerfile_WithLocalLib_RuntimeStageHasEnvDirectives()
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            localLibPath: "local");
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        var runtimeStatements = builder.Stages[1].Statements;
        var envStatements = runtimeStatements
            .Where(s => s.GetType().Name == "DockerfileEnvStatement")
            .ToList();

        Assert.Equal(2, envStatements.Count);
    }

}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileLocalLibTests
{
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
    [Fact]
    public void BuildCartonDockerfile_WithLocalLib_RuntimeStageHasEnvDirectives()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            localLibPath: "local");

        var runtimeStatements = builder.Stages[1].Statements;
        var envStatements = runtimeStatements
            .Where(s => s.GetType().Name == "DockerfileEnvStatement")
            .ToList();

        Assert.True(envStatements.Count >= 2, "Expected ENV statements for PERL5LIB and PERL_LOCAL_LIB_ROOT in runtime stage");
    }

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
}

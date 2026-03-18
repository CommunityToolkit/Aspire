using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileDeploymentTests
{
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
    [Fact]
    public void BuildCartonDockerfile_WithDeployment_RunsCartonInstallDeployment()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            localLibPath: null,
            cartonDeployment: true);

        var buildStatements = builder.Stages[0].Statements;
        var runStatements = buildStatements
            .Where(s => s.GetType().Name == "DockerfileRunStatement")
            .ToList();

        Assert.True(runStatements.Count >= 2);
    }

    [Fact]
    public void BuildCartonDockerfile_WithoutDeployment_RunsCartonInstall()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            localLibPath: null,
            cartonDeployment: false);

        var buildStatements = builder.Stages[0].Statements;
        var runStatements = buildStatements
            .Where(s => s.GetType().Name == "DockerfileRunStatement")
            .ToList();

        Assert.True(runStatements.Count >= 2);
    }

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
}

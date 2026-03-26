using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileDeploymentTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildCartonDockerfile_DeploymentFlag_ProducesRunStatements(bool cartonDeployment)
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        builder.BuildCartonDockerfile(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5",
            localLibPath: null,
            cartonDeployment: cartonDeployment);
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        var buildStatements = builder.Stages[0].Statements;
        var runStatements = buildStatements
            .Where(s => s.GetType().Name == "DockerfileRunStatement")
            .ToList();

        Assert.Equal(2, runStatements.Count);
    }
}

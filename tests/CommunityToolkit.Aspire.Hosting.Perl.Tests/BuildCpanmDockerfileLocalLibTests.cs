using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCpanmDockerfileLocalLibTests
{
    [Theory]
    [InlineData("local", 2)]
    [InlineData(null, 0)]
    public void BuildCpanmDockerfile_LocalLibControlsEnvDirectives(string? localLibPath, int minimumExpectedEnvCount)
    {
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            localLibPath: localLibPath);
#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

        var statements = builder.Stages[0].Statements;
        var envStatements = statements
            .Where(s => s.GetType().Name == "DockerfileEnvStatement")
            .ToList();

        if (minimumExpectedEnvCount == 0)
        {
            Assert.Empty(envStatements);
        }
        else
        {
            Assert.InRange(envStatements.Count, minimumExpectedEnvCount, int.MaxValue);
        }
    }

}

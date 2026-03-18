using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCpanmDockerfileLocalLibTests
{
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
    [Fact]
    public void BuildCpanmDockerfile_WithLocalLib_SetsEnvDirectives()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            localLibPath: "local");

        var statements = builder.Stages[0].Statements;
        var envStatements = statements
            .Where(s => s.GetType().Name == "DockerfileEnvStatement")
            .ToList();

        Assert.True(envStatements.Count >= 2, "Expected ENV statements for PERL5LIB and PERL_LOCAL_LIB_ROOT");
    }

    [Fact]
    public void BuildCpanmDockerfile_WithoutLocalLib_NoEnvDirectives()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        var statements = builder.Stages[0].Statements;
        var envStatements = statements
            .Where(s => s.GetType().Name == "DockerfileEnvStatement")
            .ToList();

        Assert.Empty(envStatements);
    }

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
}

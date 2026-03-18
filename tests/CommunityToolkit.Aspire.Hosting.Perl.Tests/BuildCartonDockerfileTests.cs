using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildCartonDockerfileTests
{
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
    [Fact]
    public void BuildCartonDockerfile_ProducesMultiStage()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5");

        Assert.Equal(2, builder.Stages.Count);
    }

    [Fact]
    public void BuildCartonDockerfile_BuildStageNamedBuild()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5");

        Assert.Equal("build", builder.Stages[0].StageName);
    }

    [Fact]
    public void BuildCartonDockerfile_RuntimeStageHasStatements()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCartonDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim",
            "perl:5");

        Assert.NotEmpty(builder.Stages[1].Statements);
    }

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
}

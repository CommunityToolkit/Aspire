using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREDOCKERFILEBUILDER001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class DockerfileGenerationTests
{
    #region BuildCpanmDockerfile

    [Fact]
    public void BuildCpanmDockerfile_UsesSingleStage()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        Assert.Single(builder.Stages);
    }

    [Fact]
    public void BuildCpanmDockerfile_StageHasStatements()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        var stage = builder.Stages[0];
        Assert.NotEmpty(stage.Statements);
    }

    [Fact]
    public void BuildCpanmDockerfile_CopiesDependencyManifestBeforeSource()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        var statements = builder.Stages[0].Statements;
        var copyIndexes = statements
            .Select((statement, index) => (statement, index))
            .Where(item => item.statement.GetType().Name == "DockerfileCopyStatement")
            .Select(item => item.index)
            .ToList();

        Assert.Equal(2, copyIndexes.Count);
        Assert.True(copyIndexes[0] < copyIndexes[1]);
    }

    [Fact]
    public void BuildCpanmDockerfile_InstallsDependenciesAfterManifestCopy()
    {
        var builder = new DockerfileBuilder();

        PerlAppResourceBuilderExtensions.BuildCpanmDockerfile(
            builder,
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            "perl:5-slim");

        var statements = builder.Stages[0].Statements;
        var copyIndexes = statements
            .Select((statement, index) => (statement, index))
            .Where(item => item.statement.GetType().Name == "DockerfileCopyStatement")
            .Select(item => item.index)
            .ToList();
        var runIndexes = statements
            .Select((statement, index) => (statement, index))
            .Where(item => item.statement.GetType().Name == "DockerfileRunStatement")
            .Select(item => item.index)
            .ToList();

        Assert.Equal(2, copyIndexes.Count);
        Assert.Equal(2, runIndexes.Count);
        Assert.True(copyIndexes[0] < runIndexes[1]);
        Assert.True(runIndexes[1] < copyIndexes[1]);
    }

    #endregion

    #region BuildContainerEntrypointArguments

    [Fact]
    public void BuildContainerEntrypointArguments_Script_UsesPerlEntrypoint()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            useLocalLibPath: false);

        Assert.Equal(["perl", "app.pl"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Api_IncludesDaemonSubcommand()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.API,
            "app.pl",
            apiSubcommand: "daemon",
            useLocalLibPath: false);

        Assert.Equal(["perl", "app.pl", "daemon"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Module_UsesModuleRunShape()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            useLocalLibPath: false);

        Assert.Equal(["perl", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Executable_RunsDirectly()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Executable,
            "myapp",
            apiSubcommand: null,
            useLocalLibPath: false);

        Assert.Equal(["myapp"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_ModuleWithLocalLib_IncludesIncludePath()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            useLocalLibPath: true);

        Assert.Equal(["perl", "-Ilocal/lib/perl5", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

    #endregion

    #region BuildCartonDockerfile

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

    #endregion

    #region PublishMode Wiring

    [Fact]
    public void PublishMode_ResourceExistsInModel()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", "/tmp/aspire-manifest"]);

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var allResources = appModel.Resources.ToList();
        Assert.NotEmpty(allResources);
    }

    [Fact]
    public void RunMode_DoesNotAddDockerfileAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var dockerAnnotations = resource.Annotations
            .OfType<DockerfileBuilderCallbackAnnotation>()
            .ToList();
        Assert.Empty(dockerAnnotations);
    }

    #endregion
}

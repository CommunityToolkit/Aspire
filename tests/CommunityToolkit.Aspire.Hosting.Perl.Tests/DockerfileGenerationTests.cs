using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class DockerfileGenerationTests
{
    #region BuildCpanmDockerfile

#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
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

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

    #endregion

    #region BuildContainerEntrypointArguments

#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
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

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

    #endregion

    #region BuildCartonDockerfile

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

    #endregion

    #region PublishMode Wiring

#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
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

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002

    #endregion

    #region BuildCpanmDockerfile with LocalLib

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

    #endregion

    #region BuildCartonDockerfile with LocalLib

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

    #endregion

    #region BuildCartonDockerfile deployment flag

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

    #endregion
}

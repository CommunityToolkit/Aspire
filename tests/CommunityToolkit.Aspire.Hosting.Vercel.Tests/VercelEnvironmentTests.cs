#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES003
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPROBES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using CommunityToolkit.Aspire.Hosting.Vercel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.Vercel.Tests;

public class VercelEnvironmentTests
{
    [Fact]
    public void AddVercelEnvironmentShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var action = () => builder.AddVercelEnvironment("vercel");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddVercelEnvironmentShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddVercelEnvironment(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void RunModeDoesNotAddVercelEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.DoesNotContain(model.Resources, resource => resource is VercelEnvironmentResource);
    }

    [Fact]
    public void PublishModeAddsVercelEnvironmentAndDiscoversDockerfileResource()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);

        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelScope("team")
            .WithVercelTarget("preview");

        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var api = Assert.Single(model.Resources.OfType<ContainerResource>());

        Assert.Null(api.GetComputeEnvironment());
        Assert.True(api.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile));
        Assert.Equal(sourceRoot.Path, dockerfile.ContextPath);
        Assert.Equal(Path.Combine(sourceRoot.Path, "Dockerfile"), dockerfile.DockerfilePath);
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        Assert.Same(api, entry.Resource);

        var options = environment.GetVercelOptions();
        Assert.Equal("team", options.Scope);
        Assert.Equal("preview", options.Target);
        Assert.False(options.Production);
    }

    [Fact]
    public void VercelEnvironmentOptionsCanBeConfigured()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);

        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelTarget("preview")
            .WithVercelProductionDeployments()
            .WithVercelTarget("staging");

        var options = vercel.Resource.GetVercelOptions();
        Assert.Equal("staging", options.Target);
        Assert.False(options.Production);
    }

    [Fact]
    public void WithVercelProjectNameThrowsForInvalidProjectName()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        var api = builder.AddContainer("api", "api");

        var exception = Assert.Throws<ArgumentException>(() =>
            api.WithVercelProjectName("Invalid_Project"));

        Assert.Equal("projectName", exception.ParamName);
        Assert.Contains("Use lowercase letters, digits, and hyphens", exception.Message);
    }

    [Fact]
    public async Task AddVercelEnvironmentRegistersExpectedPipelineSteps()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(new FakeVercelCliRunner());
        builder.Services.AddSingleton<IDeploymentStateManager>(new FakeDeploymentStateManager());
        var vercel = builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var stepContext = CreatePipelineStepContext(builder, app);
        var annotation = Assert.Single(environment.Annotations.OfType<PipelineStepAnnotation>());

        var steps = (await annotation.CreateStepsAsync(new()
        {
            PipelineContext = stepContext.PipelineContext,
            Resource = environment
        })).ToArray();

        foreach (var step in steps)
        {
            Assert.DoesNotContain(WellKnownPipelineSteps.Build, step.DependsOnSteps);
            Assert.DoesNotContain(WellKnownPipelineSteps.Push, step.DependsOnSteps);
        }

        Assert.Collection(
            steps,
            step =>
            {
                Assert.Equal("vercel-generate-plan-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal([WellKnownPipelineSteps.ValidateComputeEnvironments], step.DependsOnSteps);
                Assert.Equal([WellKnownPipelineSteps.Publish, WellKnownPipelineSteps.Deploy], step.RequiredBySteps);
            },
            step =>
            {
                Assert.Equal("vercel-prepare-projects-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal([WellKnownPipelineSteps.ValidateComputeEnvironments], step.DependsOnSteps);
                Assert.Equal([WellKnownPipelineSteps.Deploy], step.RequiredBySteps);
            },
            step =>
            {
                Assert.Equal("vercel-deploy-prebuilt-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal(["vercel-prepare-projects-vercel"], step.DependsOnSteps);
                Assert.Equal([WellKnownPipelineSteps.Deploy], step.RequiredBySteps);
            },
            step =>
            {
                Assert.Equal("vercel-destroy-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Empty(step.DependsOnSteps);
                Assert.Equal([WellKnownPipelineSteps.Destroy], step.RequiredBySteps);
            });
    }

    [Fact]
    public async Task VercelDeployStepDependsOnBuiltInImagePushSteps()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var api = Assert.Single(model.Resources.OfType<ContainerResource>());
        var steps = await CreateConfiguredPipelineStepsAsync(builder, app);

        var buildStep = Assert.Single(steps, step => step.Name == "build-api");
        var pushStep = Assert.Single(steps, step => step.Name == "push-api");
        var pushPrereqStep = Assert.Single(steps, step => step.Name == "push-prereq");
        var deployStep = Assert.Single(steps, step => step.Name == "vercel-deploy-prebuilt-vercel");

        AssertPipelineDependenciesAreDistinct(steps);
        Assert.Contains("vercel-prepare-projects-vercel", buildStep.DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.Deploy, buildStep.RequiredBySteps);
        Assert.Contains("vercel-prepare-projects-vercel", pushPrereqStep.DependsOnSteps);
        Assert.Contains("vercel-prepare-projects-vercel", pushStep.DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.Deploy, pushStep.RequiredBySteps);
        Assert.Contains("vercel-generate-plan-vercel", deployStep.DependsOnSteps);
        Assert.Contains("push-api", deployStep.DependsOnSteps);
        Assert.Contains(api.Annotations, annotation => annotation is ContainerImagePushOptionsCallbackAnnotation);
    }

    [Fact]
    public async Task GeneratedDockerfileResourceUsesBuiltInImageBuildAndPushSteps()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "index.html"), "hello");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");
        builder.AddDockerfileFactory("api", sourceRoot.Path, _ => Task.FromResult("""
            FROM nginx:alpine
            COPY index.html /usr/share/nginx/html/index.html
            """));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var api = Assert.Single(model.Resources.OfType<ContainerResource>());
        Assert.NotNull(Assert.Single(VercelDeploymentModel.GetEntries(model, environment)).Dockerfile!.DockerfileFactory);

        var steps = await CreateConfiguredPipelineStepsAsync(builder, app);
        var buildStep = Assert.Single(steps, step => step.Name == "build-api");
        var pushStep = Assert.Single(steps, step => step.Name == "push-api");
        var deployStep = Assert.Single(steps, step => step.Name == "vercel-deploy-prebuilt-vercel");

        AssertPipelineDependenciesAreDistinct(steps);
        Assert.Contains("vercel-prepare-projects-vercel", buildStep.DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.Deploy, buildStep.RequiredBySteps);
        Assert.Contains("vercel-prepare-projects-vercel", pushStep.DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.Deploy, pushStep.RequiredBySteps);
        Assert.Contains("vercel-generate-plan-vercel", deployStep.DependsOnSteps);
        Assert.Contains("push-api", deployStep.DependsOnSteps);
        Assert.Contains(api.Annotations, annotation => annotation is ContainerImagePushOptionsCallbackAnnotation);
    }

    [Fact]
    public async Task VercelPipelineStepActionsCanBeUnitTested()
    {
        using var sourceRoot = TemporaryDirectory.Create("pipeline-action-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://pipeline-action-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var steps = await CreateConfiguredPipelineStepsAsync(builder, app);
        var context = CreatePipelineStepContext(builder, app);
        var publishStep = Assert.Single(steps, step => step.Name == "vercel-generate-plan-vercel");
        var prereqStep = Assert.Single(steps, step => step.Name == "vercel-prepare-projects-vercel");
        var deployStep = Assert.Single(steps, step => step.Name == "vercel-deploy-prebuilt-vercel");
        var destroyStep = Assert.Single(steps, step => step.Name == "vercel-destroy-vercel");

        await publishStep.Action(context);
        await prereqStep.Action(context);
        await deployStep.Action(context);
        await destroyStep.Action(context);

        Assert.True(File.Exists(Path.Combine(outputRoot.Path, VercelConstants.DeploymentPlanFileName)));
        Assert.Contains(runner.Invocations, static invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("imagetools"));
        Assert.Contains(runner.Invocations, static invocation => invocation.FileName == "vercel" && invocation.Arguments.Contains("deploy"));
        Assert.Contains(runner.Invocations, static invocation => invocation.FileName == "vercel" && invocation.Arguments is ["project", "remove", ..]);
        Assert.NotEmpty(stateManager.SavedSections);
        Assert.NotEmpty(stateManager.DeletedSections);
        Assert.Contains(context.Summary.Items, item => item.Key == "Vercel deployment plan");
        Assert.Contains(context.Summary.Items, item => item.Key == "api Vercel deployment");
        Assert.Contains(context.Summary.Items, item => item.Key == "Vercel project removed");
    }

    [Fact]
    public async Task ValidatePrerequisitesConfiguresVcrAsDeploymentTargetRegistry()
    {
        using var sourceRoot = TemporaryDirectory.Create("vcr-registry-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var registryClient = new FakeVercelContainerRegistryClient();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(new FakeVercelCliRunner());
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(registryClient);
        builder.Services.AddSingleton<IDeploymentStateManager>(new FakeDeploymentStateManager());
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var api = Assert.Single(model.Resources.OfType<ContainerResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.ValidatePrerequisitesAsync(context, environment);

        var target = Assert.Single(api.Annotations.OfType<DeploymentTargetAnnotation>());
        Assert.Same(environment, target.DeploymentTarget);
        Assert.Same(environment, target.ComputeEnvironment);
        Assert.NotNull(target.ContainerRegistry);
        Assert.Equal("vcr.vercel.com", await target.ContainerRegistry.Endpoint.GetValueAsync(TestContext.Current.CancellationToken));
        Assert.Equal("test-team/test-project", await target.ContainerRegistry.Repository!.GetValueAsync(TestContext.Current.CancellationToken));
        Assert.Equal([ "app" ], registryClient.Repositories);

        var pushOptions = new ContainerImagePushOptions();
        var pushContext = new ContainerImagePushOptionsCallbackContext
        {
            Resource = api,
            Options = pushOptions,
            CancellationToken = TestContext.Current.CancellationToken
        };
        foreach (var annotation in api.Annotations.OfType<ContainerImagePushOptionsCallbackAnnotation>())
        {
            await annotation.Callback(pushContext);
        }

        Assert.Equal("app", pushOptions.RemoteImageName);
        Assert.StartsWith("aspire-", pushOptions.RemoteImageTag, StringComparison.Ordinal);

        var buildContext = new ContainerBuildOptionsCallbackContext(
            api,
            app.Services,
            NullLogger.Instance,
            TestContext.Current.CancellationToken,
            builder.ExecutionContext);
        foreach (var annotation in api.Annotations.OfType<ContainerBuildOptionsCallbackAnnotation>())
        {
            await annotation.Callback(buildContext);
        }

        Assert.Equal(ContainerTargetPlatform.LinuxAmd64, buildContext.TargetPlatform);
    }

    [Fact]
    public async Task VercelImageManagerLogsInBeforeEachProjectScopedPush()
    {
        var runner = new FakeVercelCliRunner();
        var inner = new VerifyingResourceContainerImageManager(runner);
        var manager = new VercelResourceContainerImageManager(inner, runner);
        var api = new ContainerResource("api");
        var backend = new ContainerResource("backend");
        api.Annotations.Add(CreatePreparedDeployment(api, "api-token", "api-project"));
        backend.Annotations.Add(CreatePreparedDeployment(backend, "backend-token", "backend-project"));

        await Task.WhenAll(
            manager.PushImageAsync(api, TestContext.Current.CancellationToken),
            manager.PushImageAsync(backend, TestContext.Current.CancellationToken));

        var loginInvocations = runner.Invocations
            .Where(static invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("login"))
            .ToArray();
        Assert.Equal(2, loginInvocations.Length);
        Assert.Contains(loginInvocations, static invocation => invocation.StandardInput == "api-token");
        Assert.Contains(loginInvocations, static invocation => invocation.StandardInput == "backend-token");
        Assert.Equal(2, inner.PushedResources.Count);
    }

    [Fact]
    public async Task DestroyPipelineStepDoesNotValidateVercelCliWhenStateIsMissing()
    {
        var runner = new FakeVercelCliRunner(new VercelCliResult(1, "", "not logged in"));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);
        var annotation = Assert.Single(environment.Annotations.OfType<PipelineStepAnnotation>());
        var steps = (await annotation.CreateStepsAsync(new()
        {
            PipelineContext = context.PipelineContext,
            Resource = environment
        })).ToArray();
        var destroyStep = Assert.Single(steps, step => step.Name == "vercel-destroy-vercel");

        await destroyStep.Action(context);

        Assert.Empty(runner.Invocations);
        var summary = Assert.Single(context.Summary.Items);
        Assert.Equal("Vercel destroy", summary.Key);
        Assert.Contains("No Vercel deployment state", summary.Value);
    }

    [Fact]
    public void WithVercelProductionDeploymentsShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<VercelEnvironmentResource> builder = null!;

        var action = () => builder.WithVercelProductionDeployments();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void PublishProjectAsDockerFileIsDiscoveredInPublishMode()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string projectPath = Path.Combine(sourceRoot.Path, "Api.csproj");
        File.WriteAllText(projectPath, "<Project />");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM mcr.microsoft.com/dotnet/aspnet:10.0");
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");
        var projectResource = new ProjectResource("api");
        var project = builder.AddResource(projectResource)
            .WithAnnotation(new FakeProjectMetadata(projectPath));

        project.PublishAsDockerFile();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var api = Assert.Single(model.Resources.OfType<ContainerResource>(), resource => resource.Name == "api");

        Assert.Null(api.GetComputeEnvironment());
        Assert.True(api.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile));
        Assert.Equal(sourceRoot.Path, dockerfile.ContextPath);
        Assert.Equal(Path.Combine(sourceRoot.Path, "Dockerfile"), dockerfile.DockerfilePath);
        Assert.Same(api, Assert.Single(VercelDeploymentModel.GetEntries(model, environment)).Resource);
    }

    [Fact]
    public void ProjectResourceIsDiscoveredInPublishMode()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string projectPath = Path.Combine(sourceRoot.Path, "Api.csproj");
        File.WriteAllText(projectPath, "<Project />");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");
        var projectResource = new ProjectResource("api");
        builder.AddResource(projectResource)
            .WithAnnotation(new FakeProjectMetadata(projectPath));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        Assert.Same(projectResource, entry.Resource);
        Assert.Equal(sourceRoot.Path, entry.SourceRoot);
        Assert.Null(entry.Dockerfile);
        Assert.Null(entry.DockerfilePath);
    }

    [Fact]
    public void PublishExecutableAsDockerFileIsDiscoveredInPublishMode()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");

        builder.AddExecutable("api", "node", sourceRoot.Path)
            .PublishAsDockerFile();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        Assert.IsAssignableFrom<ContainerResource>(entry.Resource);
        Assert.Equal(sourceRoot.Path, entry.SourceRoot);
        Assert.Equal(Path.Combine(sourceRoot.Path, "Dockerfile"), entry.DockerfilePath);
    }

    [Fact]
    public void LanguageExecutableUsesExistingGeneratedDockerfileMetadata()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "server.mjs"), "console.log('hello');");
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");
        var resource = new TestLanguageAppResource("api", "node", sourceRoot.Path);

        builder.AddResource(resource)
            .PublishAsDockerFile(container => container.WithDockerfileFactory(sourceRoot.Path, _ => Task.FromResult("""
                FROM node:22-alpine
                WORKDIR /app
                COPY server.mjs .
                CMD ["node", "server.mjs"]
                """)));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        Assert.IsAssignableFrom<ContainerResource>(entry.Resource);
        Assert.Equal(sourceRoot.Path, entry.SourceRoot);
        Assert.NotNull(entry.Dockerfile!.DockerfileFactory);
    }

    [Fact]
    public void DockerfileContainerIsDiscoveredInPublishMode()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.custom"), "FROM nginx:alpine");
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");

        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.custom");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        Assert.Equal(sourceRoot.Path, entry.SourceRoot);
        Assert.Equal(Path.Combine(sourceRoot.Path, "Dockerfile.custom"), entry.DockerfilePath);
    }

    [Fact]
    public void ExplicitComputeEnvironmentSelectsVercelResourceWhenMultipleExist()
    {
        using var apiRoot = TemporaryDirectory.Create("vercel-api-project");
        using var workerRoot = TemporaryDirectory.Create("vercel-worker-project");
        File.WriteAllText(Path.Combine(apiRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(workerRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        var vercel = builder.AddVercelEnvironment("vercel");
        var other = builder.AddVercelEnvironment("other");

        builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot.Path, "Dockerfile")
            .WithComputeEnvironment(vercel);
        builder.AddContainer("worker", "worker")
            .WithDockerfile(workerRoot.Path, "Dockerfile")
            .WithComputeEnvironment(other);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var vercelEnvironment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>(), resource => resource.Name == "vercel");
        var otherEnvironment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>(), resource => resource.Name == "other");

        var vercelEntry = Assert.Single(VercelDeploymentModel.GetEntries(model, vercelEnvironment));
        var otherEntry = Assert.Single(VercelDeploymentModel.GetEntries(model, otherEnvironment));

        Assert.Equal("api", vercelEntry.Resource.Name);
        Assert.Equal("worker", otherEntry.Resource.Name);
    }

    [Fact]
    public void UntargetedResourcesAreNotImplicitlySelectedWhenMultipleComputeEnvironmentsExist()
    {
        using var sourceRoot = TemporaryDirectory.Create("vercel-api-project");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");
        builder.AddVercelEnvironment("other");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environments = model.Resources.OfType<VercelEnvironmentResource>().ToArray();

        Assert.All(environments, environment => Assert.Empty(VercelDeploymentModel.GetEntries(model, environment)));
    }

    [Fact]
    public async Task WriteDeploymentPlanWritesExpectedJson()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        string planPath = await VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(outputRoot.Path, VercelConstants.DeploymentPlanFileName), planPath);
        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var root = document.RootElement;
        Assert.Equal("vercel", root.GetProperty("environment").GetString());
        var deployment = Assert.Single(root.GetProperty("deployments").EnumerateArray());
        Assert.Equal("api", deployment.GetProperty("resourceName").GetString());
        Assert.Equal("Dockerfile", deployment.GetProperty("dockerfilePath").GetString());
        string deployCommand = deployment.GetProperty("deployCommand").GetString()!;
        Assert.Contains("vercel pull --cwd <api-vercel-project-link> --yes --environment preview", deployCommand);
        Assert.Contains("aspire build/push api -> vcr.vercel.com/<owner>/<project>/app:<tag>", deployCommand);
        Assert.Contains("docker buildx imagetools inspect --format {{json .Manifest}} vcr.vercel.com/<owner>/<project>/app:<tag>", deployCommand);
        Assert.Contains("vercel deploy --cwd <api-build-output> --project <api-vercel-project> --prebuilt --yes", deployCommand);
    }

    [Fact]
    public async Task WriteDeploymentPlanPipelineStepWritesSummary()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var outputService = new FakePipelineOutputService(outputRoot.Path);

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IPipelineOutputService>(outputService);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.WriteDeploymentPlanAsync(context, environment);

        var summary = Assert.Single(context.Summary.Items);
        Assert.Equal("Vercel deployment plan", summary.Key);
        Assert.Equal(Path.Combine(outputRoot.Path, VercelConstants.DeploymentPlanFileName), summary.Value);
    }

    [Fact]
    public async Task WriteDeploymentPlanProcessesEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("GREETING", "hello");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        string planPath = await VercelDeploymentStep.WriteDeploymentPlanAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            model,
            environment,
            outputRoot.Path,
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var deployment = Assert.Single(document.RootElement.GetProperty("deployments").EnumerateArray());

        string deployCommand = deployment.GetProperty("deployCommand").GetString()!;
        Assert.Contains("aspire build/push api -> vcr.vercel.com/<owner>/<project>/app:<tag>", deployCommand);
        Assert.Contains("vercel deploy --cwd <api-build-output> --project <api-vercel-project> --prebuilt --yes --env GREETING=<value>", deployCommand);
        Assert.Equal("GREETING", Assert.Single(deployment.GetProperty("environmentVariables").EnumerateArray()).GetString());
        Assert.DoesNotContain("hello", File.ReadAllText(planPath));
    }

    [Fact]
    public async Task WriteDeploymentPlanDoesNotResolveSecretEnvironmentVariableValues()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        var secret = builder.AddParameter("api-key", secret: true);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("API_KEY", secret);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        string planPath = await VercelDeploymentStep.WriteDeploymentPlanAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            model,
            environment,
            outputRoot.Path,
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var deployment = Assert.Single(document.RootElement.GetProperty("deployments").EnumerateArray());

        Assert.DoesNotContain("--env API_KEY", deployment.GetProperty("deployCommand").GetString(), StringComparison.Ordinal);
        Assert.Equal("API_KEY", Assert.Single(deployment.GetProperty("environmentVariables").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsWhenDockerfileIsMissing()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("Dockerfile", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanAllowsGeneratedDockerfileFactoryBeforeBuild()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        Assert.False(File.Exists(Path.Combine(sourceRoot.Path, "Dockerfile")));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddDockerfileFactory("api", sourceRoot.Path, _ => Task.FromResult("""
            FROM nginx:alpine
            COPY index.html /usr/share/nginx/html/index.html
            """));
        File.WriteAllText(Path.Combine(sourceRoot.Path, "index.html"), "hello");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        string planPath = await VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(planPath));
        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var deployment = Assert.Single(document.RootElement.GetProperty("deployments").EnumerateArray());
        Assert.Equal("<generated>", deployment.GetProperty("dockerfilePath").GetString());
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsWhenSourceRootIsMissing()
    {
        using var outputRoot = TemporaryDirectory.Create();
        string sourceRoot = Path.Combine(Path.GetTempPath(), $"missing-vercel-tests-{Guid.NewGuid():N}");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains(sourceRoot, exception.Message);
        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsWhenContainerHasNoSourceRoot()
    {
        using var outputRoot = TemporaryDirectory.Create();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("is not an Aspire image build resource", exception.Message);
        Assert.Contains("WithDockerfile", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForContainerMounts()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.WithVolume("/data"));

        Assert.Contains("volumes or bind mounts", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForContainerFiles()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.Resource.Annotations.Add(new ContainerFilesSourceAnnotation { SourcePath = "." }));

        Assert.Contains("container file mounts", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForHealthChecksAndProbes()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.WithEndpoint(targetPort: 8080, scheme: "http", name: "http", isExternal: true)
                .WithHttpProbe(ProbeType.Readiness, path: "/health"));

        Assert.Contains("health checks or container probes", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForReplicas()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.Resource.Annotations.Add(new ReplicaAnnotation(2)));

        Assert.Contains("replicas or scale", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanIgnoresWaitDependencies()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithVercelProjectName("backend");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithVercelProjectName("api")
            .WaitFor(backend);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        string planPath = await VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var deployments = document.RootElement.GetProperty("deployments")
            .EnumerateArray()
            .Select(static deployment => deployment.GetProperty("resourceName").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["api", "backend"], deployments);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForGenericContainerRegistry()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        var registry = builder.AddContainerRegistry("registry", "registry.example.com");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithContainerRegistry(registry);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("generic container registry/image push metadata", exception.Message);
        Assert.Contains("WithContainerRegistry", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForMultipleEndpointTargetPorts()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.WithEndpoint(targetPort: 8080, scheme: "http", name: "http", isExternal: true)
                .WithEndpoint(targetPort: 8081, scheme: "http", name: "admin", isExternal: true));

        Assert.Contains("multiple Aspire endpoint target ports", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForNonHttpEndpoints()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.WithEndpoint(targetPort: 6379, scheme: "tcp", name: "tcp", isExternal: true));

        Assert.Contains("support only HTTP or HTTPS endpoints", exception.Message);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    public async Task WriteDeploymentPlanAllowsExternalHttpEndpoints(string scheme)
    {
        await AssertWriteDeploymentPlanSucceedsAsync(api =>
            api.WithEndpoint(targetPort: 8080, scheme: scheme, name: scheme, isExternal: true));
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForInternalHttpEndpoints()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.WithEndpoint(targetPort: 8080, scheme: "http", name: "http", isExternal: false));

        Assert.Contains("public platform HTTPS ingress only", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForNonHttpEndpointTransports()
    {
        var exception = await AssertWriteDeploymentPlanThrowsAsync(static api =>
            api.Resource.Annotations.Add(new EndpointAnnotation(
                ProtocolType.Tcp,
                uriScheme: "http",
                transport: "tcp",
                name: "tcp-http",
                targetPort: 8080,
                isExternal: true)));

        Assert.Contains("transport 'tcp'", exception.Message);
        Assert.Contains("HTTP transports", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForManagedProjectNameCollisions()
    {
        using var parent = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        string firstRoot = Path.Combine(parent.Path, "my.api");
        string secondRoot = Path.Combine(parent.Path, "my_api");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        File.WriteAllText(Path.Combine(firstRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(secondRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(firstRoot, "Dockerfile");
        builder.AddContainer("worker", "worker")
            .WithDockerfile(secondRoot, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("project name 'my-api'", exception.Message);
        Assert.Contains("'api'", exception.Message);
        Assert.Contains("'worker'", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForLinkedProjectNameCollisions()
    {
        using var parent = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        string firstRoot = Path.Combine(parent.Path, "api");
        string secondRoot = Path.Combine(parent.Path, "worker");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        File.WriteAllText(Path.Combine(firstRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(secondRoot, "Dockerfile"), "FROM nginx:alpine");
        WriteVercelProjectLink(firstRoot, "shared-project", "prj_shared_a");
        WriteVercelProjectLink(secondRoot, "shared-project", "prj_shared_b");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(firstRoot, "Dockerfile");
        builder.AddContainer("worker", "worker")
            .WithDockerfile(secondRoot, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("project name 'shared-project'", exception.Message);
        Assert.Contains("'api'", exception.Message);
        Assert.Contains("'worker'", exception.Message);
    }

    [Fact]
    public async Task WriteDeploymentPlanThrowsForConfiguredProjectNameCollisions()
    {
        using var parent = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        string firstRoot = Path.Combine(parent.Path, "api");
        string secondRoot = Path.Combine(parent.Path, "worker");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        File.WriteAllText(Path.Combine(firstRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(secondRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(firstRoot, "Dockerfile")
            .WithVercelProjectName("shared-project");
        builder.AddContainer("worker", "worker")
            .WithDockerfile(secondRoot, "Dockerfile")
            .WithVercelProjectName("shared-project");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("project name 'shared-project'", exception.Message);
        Assert.Contains("WithVercelProjectName", exception.Message);
        Assert.Contains("'api'", exception.Message);
        Assert.Contains("'worker'", exception.Message);
    }

    [Fact]
    public async Task DeployAsyncUsesOriginalSourceRootWithSlugifiedProjectName()
    {
        using var sourceRoot = TemporaryDirectory.Create("Invalid_Project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://invalid-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var digestInvocation = Assert.Single(runner.Invocations, invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("imagetools"));
        Assert.Null(digestInvocation.WorkingDirectory);
        Assert.StartsWith("vcr.vercel.com/test-team/test-project/app:", digestInvocation.Arguments[^1], StringComparison.Ordinal);
        var deployInvocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("deploy"));
        Assert.Contains("--project", deployInvocation.Arguments);
        Assert.Contains("invalid-project", deployInvocation.Arguments);

        var deployment = Assert.Single(ReadSavedState(stateManager.SavedSections[^1]).Deployments);
        Assert.Equal("invalid-project", deployment.ProjectName);
    }

    [Fact]
    public async Task DeployAsyncUsesGeneratedLocalConfigWithConfiguredProjectName()
    {
        using var sourceRoot = TemporaryDirectory.Create("source-folder");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://configured-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithVercelProjectName("configured-project");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        string expectedDeployDirectory = Path.Combine(tempRoot.Path, "api", "vercel-build-output");
        AssertGeneratedBuildOutput(expectedDeployDirectory);
        Assert.False(File.Exists(Path.Combine(sourceRoot.Path, "vercel.json")));
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.Contains("link"));
        var deployInvocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("deploy"));
        Assert.Equal(expectedDeployDirectory, deployInvocation.WorkingDirectory);
        Assert.Contains("--project", deployInvocation.Arguments);
        Assert.Contains("configured-project", deployInvocation.Arguments);
        Assert.DoesNotContain("--local-config", deployInvocation.Arguments);

        var state = ReadSavedState(stateManager.SavedSections[^1]);
        var deployment = Assert.Single(state.Deployments);
        Assert.Equal("configured-project", deployment.ProjectName);
        Assert.True(deployment.ManagedByAspire);
    }

    [Fact]
    public void BuildDeployArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Production = true,
            Scope = "team"
        };
        var entry = CreateDeploymentEntry("/repo/src/api");

        string[] arguments = VercelCliArguments.BuildDeployArguments(options, entry);

        Assert.Equal(["deploy", "--scope", "team", "--cwd", "/repo/src/api", "--project", "api", "--prebuilt", "--yes", "--prod"], arguments);
    }

    [Fact]
    public void BuildDockerInspectDigestArgumentsUsesBuildxImagetools()
    {
        string[] arguments = VercelCliArguments.BuildDockerInspectDigestArguments("vcr.vercel.com/team/project/app:tag");

        Assert.Equal(["buildx", "imagetools", "inspect", "--format", "{{json .Manifest}}", "vcr.vercel.com/team/project/app:tag"], arguments);
    }

    [Fact]
    public void GetDockerImageDigestParsesJsonString()
    {
        string digest = VercelDockerImageDigestParser.GetDigest($"\"{FakeVercelCliRunner.FakeImageDigest}\"");

        Assert.Equal(FakeVercelCliRunner.FakeImageDigest, digest);
    }

    [Fact]
    public void GetDockerImageDigestSelectsLinuxAmd64ManifestDigest()
    {
        string digest = VercelDockerImageDigestParser.GetDigest(FakeVercelCliRunner.FakeImageManifestJson);

        Assert.Equal(FakeVercelCliRunner.FakeImageDigest, digest);
    }

    [Fact]
    public void GetDockerImageDigestThrowsWhenManifestDoesNotContainLinuxAmd64Digest()
    {
        const string manifestJson = """
            {
              "schemaVersion": 2,
              "manifests": [
                {
                  "digest": "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                  "platform": {
                    "architecture": "arm64",
                    "os": "linux"
                  }
                }
              ]
            }
            """;

        var exception = Assert.Throws<DistributedApplicationException>(() =>
            VercelDockerImageDigestParser.GetDigest(manifestJson));

        Assert.Contains("linux/amd64 manifest digest", exception.Message);
    }

    [Fact]
    public void DecodeUnvalidatedOidcClaimsParsesCompactJwtPayload()
    {
        string token = CreateTestJwt("""
            {
              "owner_id": "team_123",
              "owner": "test-team",
              "project": "test-project",
              "project_id": "prj_123"
            }
            """);

        var claims = VercelOidcToken.DecodeUnvalidatedClaims(token);

        Assert.Equal("team_123", claims.OwnerId);
        Assert.Equal("test-team", claims.Owner);
        Assert.Equal("test-project", claims.Project);
        Assert.Equal("prj_123", claims.ProjectId);
    }

    [Fact]
    public void DecodeUnvalidatedOidcClaimsRejectsNonCompactJwt()
    {
        var exception = Assert.Throws<DistributedApplicationException>(() =>
            VercelOidcToken.DecodeUnvalidatedClaims("not-a-jwt"));

        Assert.Contains("compact JWT", exception.Message);
    }

    [Fact]
    public void ParseDotEnvFileParsesVercelPullOutputSubset()
    {
        var values = VercelDotEnvParser.Parse(
        [
            "",
            "# comment",
            "VERCEL_OIDC_TOKEN=\"header.payload.signature\"",
            "MULTILINE=\"line1\\nline2\"",
            "QUOTED='single quoted'",
            "ESCAPED=\"quote\\\"slash\\\\\"",
            "MALFORMED"
        ]);

        Assert.Equal("header.payload.signature", values["VERCEL_OIDC_TOKEN"]);
        Assert.Equal("line1\nline2", values["MULTILINE"]);
        Assert.Equal("single quoted", values["QUOTED"]);
        Assert.Equal("quote\"slash\\", values["ESCAPED"]);
        Assert.DoesNotContain("MALFORMED", values.Keys);
    }

    [Fact]
    public void ProjectListContainsProjectUsesExactJsonProjectName()
    {
        Assert.True(VercelCliOutputParser.ProjectListContainsProject(ProjectListJson("api", "worker"), "api"));
        Assert.False(VercelCliOutputParser.ProjectListContainsProject(ProjectListJson("api-preview"), "api"));
    }

    [Fact]
    public void EnvironmentVariableListContainsNameUsesExactJsonKeyAndSkipsBranchScopedVariables()
    {
        const string output = """
            {
              "envs": [
                {
                  "key": "API_KEY",
                  "target": ["preview"],
                  "gitBranch": "feature"
                },
                {
                  "key": "OLD_KEY",
                  "target": ["preview"]
                }
              ]
            }
            """;

        Assert.False(VercelCliOutputParser.EnvironmentVariableListContainsName(output, "API_KEY"));
        Assert.True(VercelCliOutputParser.EnvironmentVariableListContainsName(output, "OLD_KEY"));
        Assert.False(VercelCliOutputParser.EnvironmentVariableListContainsName(output, "KEY"));
    }

    [Fact]
    public void BuildProjectEnvironmentVariableArgumentsUseLinkedScratchDirectory()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] linkArguments = VercelCliArguments.BuildLinkProjectArguments(options, "/tmp/vercel-link", "api-project");
        string[] envArguments = VercelCliArguments.BuildAddProjectEnvironmentVariableArguments(options, "/tmp/vercel-link", "API_KEY", "production");
        string[] removeEnvArguments = VercelCliArguments.BuildRemoveProjectEnvironmentVariableArguments(options, "/tmp/vercel-link", "API_KEY", "production");
        string[] listEnvArguments = VercelCliArguments.BuildListProjectEnvironmentVariablesArguments(options, "/tmp/vercel-link", "production");

        Assert.Equal(["link", "--scope", "team", "--cwd", "/tmp/vercel-link", "--yes", "--project", "api-project"], linkArguments);
        Assert.Equal(["env", "add", "API_KEY", "production", "--scope", "team", "--cwd", "/tmp/vercel-link", "--yes", "--force", "--sensitive"], envArguments);
        Assert.Equal(["env", "rm", "API_KEY", "production", "--scope", "team", "--cwd", "/tmp/vercel-link", "--yes"], removeEnvArguments);
        Assert.Equal(["env", "ls", "production", "--scope", "team", "--cwd", "/tmp/vercel-link", "--format=json"], listEnvArguments);
        Assert.DoesNotContain("--project", envArguments);
    }

    [Fact]
    public void BuildDeployArgumentsIncludesTarget()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Target = "preview"
        };
        var entry = CreateDeploymentEntry("/repo/src/api");

        string[] arguments = VercelCliArguments.BuildDeployArguments(options, entry);

        Assert.Equal(["deploy", "--cwd", "/repo/src/api", "--project", "api", "--prebuilt", "--yes", "--target", "preview"], arguments);
    }

    [Fact]
    public void BuildDestroyProjectArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] arguments = VercelCliArguments.BuildDestroyProjectArguments(options, "api");

        Assert.Equal(["project", "remove", "api", "--scope", "team"], arguments);
    }

    [Fact]
    public void BuildValidateScopeArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] arguments = VercelCliArguments.BuildValidateScopeArguments(options);

        Assert.Equal(["project", "ls", "--scope", "team", "--format=json"], arguments);
    }

    [Fact]
    public void BuildListProjectsArgumentsIncludesFilter()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] arguments = VercelCliArguments.BuildListProjectsArguments(options, "api");

        Assert.Equal(["project", "ls", "--scope", "team", "--filter", "api", "--format=json"], arguments);
    }

    [Fact]
    public void BuildInspectDeploymentArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] arguments = VercelCliArguments.BuildInspectDeploymentArguments(options, "https://api.vercel.app");

        Assert.Equal(["inspect", "https://api.vercel.app", "--scope", "team", "--wait", "--timeout", "120s", "--format=json"], arguments);
    }

    [Fact]
    public async Task BuildDeployArgumentsProcessesEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("GREETING", "hello");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            TestContext.Current.CancellationToken);

        Assert.Contains("--env", arguments);
        Assert.Contains("GREETING=hello", arguments);
    }

    [Fact]
    public async Task BuildDeployArgumentsAllowsNonSecretParameterEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var region = builder.AddParameter("region", "iad");
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("REGION", region);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            TestContext.Current.CancellationToken);

        Assert.Contains("--env", arguments);
            Assert.Contains(arguments, argument => argument.StartsWith("REGION=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForContainerEntrypoint()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        builder.AddVercelEnvironment("vercel");
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");
        api.Resource.Entrypoint = "node";

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("entrypoint", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsDoesNotPassSecretEnvironmentVariablesOnCommandLine()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var secret = builder.AddParameter("api-key", "secret-value", secret: true);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("API_KEY", secret);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(arguments, argument => argument.Contains("API_KEY", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeployAsyncConfiguresSecretEnvironmentVariablesBeforeDeploy()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, """
                {
                  "deployment": {
                    "id": "dpl_secret",
                    "url": "https://secret-project.vercel.app"
                  }
                }
                """, ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var secret = builder.AddParameter("api-key", "secret-value", secret: true);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("AUTH_HEADER", ReferenceExpression.Create($"Bearer {secret.Resource}"))
            .WithEnvironment("API_KEY", secret)
            .WithEnvironment("GREETING", "hello");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        string projectName = VercelProjectNameResolver.GetProjectName(entry);
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        string expectedProjectLinkDirectory = Path.Combine(tempRoot.Path, "api", ".vercel-project");
        string expectedDeployDirectory = Path.Combine(tempRoot.Path, "api", "vercel-build-output");
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(["project", "add", projectName]));
        var linkInvocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("link"));
        Assert.Equal(["link", "--cwd", expectedProjectLinkDirectory, "--yes", "--project", projectName], linkInvocation.Arguments);
        Assert.Equal(expectedProjectLinkDirectory, linkInvocation.WorkingDirectory);
        var apiKeyInvocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("API_KEY"));
        Assert.Equal(["env", "add", "API_KEY", "production", "--cwd", expectedProjectLinkDirectory, "--yes", "--force", "--sensitive"], apiKeyInvocation.Arguments);
        Assert.Equal("secret-value", apiKeyInvocation.StandardInput);
        Assert.Equal(expectedProjectLinkDirectory, apiKeyInvocation.WorkingDirectory);
        var authHeaderInvocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("AUTH_HEADER"));
        Assert.Equal(["env", "add", "AUTH_HEADER", "production", "--cwd", expectedProjectLinkDirectory, "--yes", "--force", "--sensitive"], authHeaderInvocation.Arguments);
        Assert.Equal("Bearer secret-value", authHeaderInvocation.StandardInput);
        Assert.Equal(expectedProjectLinkDirectory, authHeaderInvocation.WorkingDirectory);
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(["pull", "--cwd", expectedProjectLinkDirectory, "--yes", "--environment", "production"]));
        Assert.Contains(runner.Invocations, invocation => invocation.FileName == "docker" && invocation.Arguments.SequenceEqual(["login", "vcr.vercel.com", "--username", "team_test", "--password-stdin"]));
        Assert.Contains(runner.Invocations, invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("imagetools"));
        AssertGeneratedBuildOutput(expectedDeployDirectory);
        var deployInvocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("deploy"));
        Assert.Contains("GREETING=hello", deployInvocation.Arguments);
        Assert.DoesNotContain(deployInvocation.Arguments, argument => argument.Contains("API_KEY", StringComparison.Ordinal));
        Assert.DoesNotContain(deployInvocation.Arguments, argument => argument.Contains("AUTH_HEADER", StringComparison.Ordinal));

        var deployment = Assert.Single(ReadSavedState(stateManager.SavedSections[^1]).Deployments);
        Assert.Equal(["API_KEY", "AUTH_HEADER"], deployment.ProjectEnvironmentVariables);
        Assert.DoesNotContain("secret-value", JsonSerializer.Serialize(ReadSavedState(stateManager.SavedSections[^1])), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeployAsyncRemovesStaleProjectEnvironmentVariablesFromPreviousState()
    {
        using var sourceRoot = TemporaryDirectory.Create("stale-env-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://stale-env-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var secret = builder.AddParameter("api-key", "secret-value", secret: true);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("API_KEY", secret);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        string projectName = VercelProjectNameResolver.GetProjectName(entry);
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            SchemaVersion: 1,
            Environment: "vercel",
            Scope: null,
            Target: null,
            Production: false,
            Deployments:
            [
                new(
                    ResourceName: "api",
                    ProjectName: projectName,
                    ProjectId: null,
                    DeploymentId: "dpl_previous",
                    DeploymentUrl: "https://previous.vercel.app",
                    SourceRoot: sourceRoot.Path,
                    ManagedByAspire: true)
                {
                    ProjectEnvironmentVariables = ["API_KEY", "OLD_KEY"]
                }
            ]), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        string expectedProjectLinkDirectory = Path.Combine(tempRoot.Path, "api", ".vercel-project");
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(["env", "rm", "OLD_KEY", "preview", "--cwd", expectedProjectLinkDirectory, "--yes"]));
        Assert.DoesNotContain(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(["env", "rm", "API_KEY", "preview", "--cwd", expectedProjectLinkDirectory, "--yes"]));
        var deployment = Assert.Single(ReadSavedState(stateManager.SavedSections[^1]).Deployments);
        Assert.Equal(["API_KEY"], deployment.ProjectEnvironmentVariables);
    }

    [Fact]
    public async Task ValidatePrerequisitesPreservesPreviousProjectEnvironmentVariablesUntilDeploySucceeds()
    {
        using var sourceRoot = TemporaryDirectory.Create("retry-safe-env-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner();
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var secret = builder.AddParameter("api-key", "secret-value", secret: true);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("API_KEY", secret);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        string projectName = VercelProjectNameResolver.GetProjectName(entry);
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            SchemaVersion: 1,
            Environment: "vercel",
            Scope: null,
            Target: null,
            Production: false,
            Deployments:
            [
                new(
                    ResourceName: "api",
                    ProjectName: projectName,
                    ProjectId: null,
                    DeploymentId: "dpl_previous",
                    DeploymentUrl: "https://previous.vercel.app",
                    SourceRoot: sourceRoot.Path,
                    ManagedByAspire: true)
                {
                    ProjectEnvironmentVariables = ["API_KEY", "OLD_KEY"]
                }
            ]), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.ValidatePrerequisitesAsync(context, environment);

        string expectedProjectLinkDirectory = Path.Combine(tempRoot.Path, "api", ".vercel-project");
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(["env", "rm", "OLD_KEY", "preview", "--cwd", expectedProjectLinkDirectory, "--yes"]));
        var initialState = ReadSavedState(Assert.Single(stateManager.SavedSections));
        var deployment = Assert.Single(initialState.Deployments);
        Assert.Equal(["API_KEY", "OLD_KEY"], deployment.ProjectEnvironmentVariables);
    }

    [Fact]
    public async Task BuildDeployArgumentsDoesNotPassConnectionStringEnvironmentVariablesOnCommandLine()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        builder.Configuration["ConnectionStrings:db"] = "Host=example.com;Username=app;Password=secret";
        var connectionString = builder.AddConnectionString("db");
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("DATABASE_URL", connectionString);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(arguments, argument => argument.Contains("DATABASE_URL", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForInvalidEnvironmentVariableNames()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("INVALID-NAME", "value");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("INVALID-NAME", exception.Message);
        Assert.Contains("invalid Vercel environment variable name", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsUsesProductionUrlForEndpointEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(sourceRoot.Path, "api-app");
        string backendRoot = Path.Combine(sourceRoot.Path, "backend-app");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(backendRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(backendRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile")
            .WithComputeEnvironment(vercel);
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(backendRoot, "Dockerfile")
            .WithEndpoint(port: 8080, targetPort: 8080, scheme: "http", name: "http", isExternal: true)
            .WithComputeEnvironment(vercel);
        api.WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            entries,
            TestContext.Current.CancellationToken);

        Assert.Contains("BACKEND_URL=https://backend-app.vercel.app", arguments);
    }

    [Fact]
    public async Task BuildDeployArgumentsUsesProductionUrlForServiceDiscoveryReferences()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(sourceRoot.Path, "api-app");
        string backendRoot = Path.Combine(sourceRoot.Path, "backend-app");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(backendRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(backendRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(backendRoot, "Dockerfile")
            .WithEndpoint(port: 8080, targetPort: 8080, scheme: "http", name: "http", isExternal: true)
            .WithComputeEnvironment(vercel);
        builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile")
            .WithReference(backend.GetEndpoint("http"))
            .WithComputeEnvironment(vercel);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            entries,
            TestContext.Current.CancellationToken);

        Assert.Contains("BACKEND_HTTP=https://backend-app.vercel.app", arguments);
    }

    [Fact]
    public async Task BuildDeployArgumentsUsesConfiguredProjectNameForEndpointReferences()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(sourceRoot.Path, "api-app");
        string backendRoot = Path.Combine(sourceRoot.Path, "backend-source");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(backendRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(backendRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile")
            .WithComputeEnvironment(vercel);
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(backendRoot, "Dockerfile")
            .WithEndpoint(port: 8080, targetPort: 8080, scheme: "http", name: "http", isExternal: true)
            .WithVercelProjectName("configured-backend")
            .WithComputeEnvironment(vercel);
        api.WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            entries,
            TestContext.Current.CancellationToken);

        Assert.Contains("BACKEND_URL=https://configured-backend.vercel.app", arguments);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForEndpointReferencesWithoutProductionUrls()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(sourceRoot.Path, "api-app");
        string backendRoot = Path.Combine(sourceRoot.Path, "backend-app");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(backendRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(backendRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelTarget("preview");
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile")
            .WithComputeEnvironment(vercel);
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(backendRoot, "Dockerfile")
            .WithEndpoint(port: 8080, targetPort: 8080, scheme: "http", name: "http", isExternal: true)
            .WithComputeEnvironment(vercel);
        api.WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                entries,
                TestContext.Current.CancellationToken));

        Assert.Contains("Vercel endpoint references require production deployments", exception.ToString());
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForEndpointReferencesToDifferentVercelEnvironment()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(sourceRoot.Path, "api-app");
        string backendRoot = Path.Combine(sourceRoot.Path, "backend-app");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(backendRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(backendRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        var otherVercel = builder.AddVercelEnvironment("other-vercel")
            .WithVercelProductionDeployments();
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile")
            .WithComputeEnvironment(vercel);
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(backendRoot, "Dockerfile")
            .WithEndpoint(port: 8080, targetPort: 8080, scheme: "http", name: "http", isExternal: true)
            .WithComputeEnvironment(otherVercel);
        api.WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>(), environment => environment.Name == "vercel");
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                entries,
                TestContext.Current.CancellationToken));

        Assert.Contains("does not target this Vercel environment", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForInternalEndpointReferences()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(sourceRoot.Path, "api-app");
        string backendRoot = Path.Combine(sourceRoot.Path, "backend-app");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(backendRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(backendRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile")
            .WithComputeEnvironment(vercel);
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(backendRoot, "Dockerfile")
            .WithEndpoint(port: 8080, targetPort: 8080, scheme: "http", name: "http", isExternal: false)
            .WithComputeEnvironment(vercel);
        api.WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                entries,
                TestContext.Current.CancellationToken));

        Assert.Contains("can only target external HTTP or HTTPS endpoints", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForTargetPortEndpointReferenceWithoutExplicitTargetPort()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(sourceRoot.Path, "api-app");
        string backendRoot = Path.Combine(sourceRoot.Path, "backend-app");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(backendRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(backendRoot, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile")
            .WithComputeEnvironment(vercel);
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(backendRoot, "Dockerfile")
            .WithEndpoint(port: 8080, scheme: "http", name: "http", isExternal: true)
            .WithComputeEnvironment(vercel);
        api.WithEnvironment("BACKEND_TARGET_PORT", backend.GetEndpoint("http").Property(EndpointProperty.TargetPort));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entries = VercelDeploymentModel.GetEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                entries,
                TestContext.Current.CancellationToken));

        Assert.Contains("does not define an explicit target port", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForCommandLineArguments()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithArgs("--verbose");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("command-line arguments", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsAllowsDockerBuildArguments()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithBuildArg("FOO", "bar");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));

        string[] arguments = await VercelDeploymentPlanWriter.BuildDeployArgumentsAsync(
            builder.ExecutionContext,
            NullLogger.Instance,
            environment.GetVercelOptions(),
            entry,
            TestContext.Current.CancellationToken);

        Assert.Contains("--prebuilt", arguments);
    }

    [Fact]
    public async Task DeployAsyncDoesNotBuildOrPushImagesItself()
    {
        using var sourceRoot = TemporaryDirectory.Create("registry-free-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://registry-free-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();
        var imageManager = new ThrowingResourceContainerImageManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.Services.AddSingleton<IResourceContainerImageManager>(imageManager);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        Assert.Equal(0, imageManager.CallCount);
        Assert.Equal(11, runner.Invocations.Count);
        Assert.Equal(2, stateManager.SavedSections.Count);
    }

    [Fact]
    public async Task DeployAsyncRunsVercelCliAndSavesDeploymentState()
    {
        using var sourceRoot = TemporaryDirectory.Create("vercel-state-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, """
            {
              "deployment": {
                "id": "dpl_123",
                "url": "https://api.vercel.app"
              }
            }
            """, ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelScope("team")
            .WithVercelProductionDeployments();
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("GREETING", "hello");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var digestInvocation = Assert.Single(runner.Invocations, invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("imagetools"));
        var invocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("deploy"));
        string expectedDeployDirectory = Path.Combine(tempRoot.Path, "api", "vercel-build-output");
        Assert.Equal(VercelCliArguments.BuildDockerInspectDigestArguments(digestInvocation.Arguments[^1]), digestInvocation.Arguments);
        Assert.StartsWith("vcr.vercel.com/test-team/test-project/app:", digestInvocation.Arguments[^1], StringComparison.Ordinal);
        Assert.Equal("vercel", invocation.FileName);
        Assert.Equal(expectedDeployDirectory, invocation.WorkingDirectory);
        Assert.Equal(["deploy", "--scope", "team", "--cwd", expectedDeployDirectory, "--project", "vercel-state-project", "--prebuilt", "--yes", "--prod", "--env", "GREETING=hello"], invocation.Arguments);
        Assert.Null(invocation.StandardInput);
        AssertGeneratedBuildOutput(expectedDeployDirectory);
        Assert.False(File.Exists(Path.Combine(sourceRoot.Path, "vercel.json")));
        var inspectInvocation = Assert.Single(runner.Invocations, invocation => invocation.FileName == "vercel" && invocation.Arguments.Contains("inspect"));
        Assert.Equal(["inspect", "https://api.vercel.app", "--scope", "team", "--wait", "--timeout", "120s", "--format=json"], inspectInvocation.Arguments);

        Assert.Collection(
            context.Summary.Items,
            summary =>
            {
                Assert.Equal("api Vercel deployment", summary.Key);
                Assert.Equal("https://api.vercel.app", summary.Value);
            },
            summary =>
            {
                Assert.Equal("api Vercel production URL", summary.Key);
                Assert.Equal("https://vercel-state-project.vercel.app", summary.Value);
            });

        Assert.Equal(2, stateManager.SavedSections.Count);
        var savedSection = stateManager.SavedSections[^1];
        Assert.Equal("communitytoolkit.vercel.vercel", savedSection.SectionName);
        string stateJson = savedSection.Data.First().Value!.GetValue<string>();
        Assert.Contains("schemaVersion", stateJson);
        Assert.Contains("\"scope\": \"team\"", stateJson);
        Assert.Contains("vercel-state-project", stateJson);
        Assert.Contains("dpl_123", stateJson);
        Assert.Contains("managedByAspire", stateJson);
        var state = ReadSavedState(savedSection);
        var deployment = Assert.Single(state.Deployments);
        Assert.True(state.Production);
        Assert.Equal("https://vercel-state-project.vercel.app", deployment.ProductionUrl);
        Assert.Equal(FakeVercelCliRunner.FakeImageDigest, deployment.VcrImageDigest);
        Assert.Equal(3, deployment.BuildOutputApiVersion);
    }

    [Fact]
    public async Task DeployAsyncSavesVerifiedResourcesBeforeLaterResourceFails()
    {
        using var parent = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        string apiRoot = Path.Combine(parent.Path, "partial-api");
        string workerRoot = Path.Combine(parent.Path, "partial-worker");
        Directory.CreateDirectory(apiRoot);
        Directory.CreateDirectory(workerRoot);
        File.WriteAllText(Path.Combine(apiRoot, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(workerRoot, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new(0, "https://partial-api.vercel.app", ""),
            ReadyInspectResult(),
            new(1, "", "worker deploy failed"));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(apiRoot, "Dockerfile");
        builder.AddContainer("worker", "worker")
            .WithDockerfile(workerRoot, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("worker deploy failed", exception.Message);
        var state = ReadSavedState(stateManager.SavedSections[^1]);
        Assert.Collection(
            state.Deployments.OrderBy(static deployment => deployment.ResourceName),
            deployment =>
            {
                Assert.Equal("api", deployment.ResourceName);
                Assert.Equal("partial-api", deployment.ProjectName);
                Assert.Equal("https://partial-api.vercel.app", deployment.DeploymentUrl);
            },
            deployment =>
            {
                Assert.Equal("worker", deployment.ResourceName);
                Assert.Equal("partial-worker", deployment.ProjectName);
                Assert.Null(deployment.DeploymentUrl);
            });
    }

    [Fact]
    public async Task DeployAsyncPreservesPreviousManagedStateForResourcesRemovedFromModel()
    {
        using var sourceRoot = TemporaryDirectory.Create("current-api");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new(0, "https://current-api.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            null,
            null,
            false,
            [
                new("worker", "removed-worker", "prj_worker", "dpl_worker", "https://removed-worker.vercel.app", "/src/worker", true)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var state = ReadSavedState(stateManager.SavedSections[^1]);
        Assert.Collection(
            state.Deployments.OrderBy(static deployment => deployment.ResourceName),
            deployment =>
            {
                Assert.Equal("api", deployment.ResourceName);
                Assert.Equal("current-api", deployment.ProjectName);
            },
            deployment =>
            {
                Assert.Equal("worker", deployment.ResourceName);
                Assert.Equal("removed-worker", deployment.ProjectName);
            });
    }

    [Fact]
    public async Task DeployAsyncMarksLinkedVercelProjectsAsUnmanaged()
    {
        using var sourceRoot = TemporaryDirectory.Create("linked-project-source");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, ".vercel"));
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".vercel", "cache.json"), "{}");
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".vercel", "project.json"), """
            {
              "projectId": "prj_linked",
              "projectName": "linked-project"
            }
            """);
        string linkedProjectJson = File.ReadAllText(Path.Combine(sourceRoot.Path, ".vercel", "project.json"));
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://linked-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var state = ReadSavedState(stateManager.SavedSections[^1]);
        var deployment = Assert.Single(state.Deployments);
        Assert.Equal("linked-project", deployment.ProjectName);
        Assert.Equal("prj_linked", deployment.ProjectId);
        Assert.False(deployment.ManagedByAspire);

        var deployInvocation = Assert.Single(runner.Invocations, invocation => invocation.Arguments.Contains("deploy"));
        Assert.Equal(Path.Combine(tempRoot.Path, "api", "vercel-build-output"), deployInvocation.WorkingDirectory);
        Assert.Contains("--project", deployInvocation.Arguments);
        Assert.Contains("prj_linked", deployInvocation.Arguments);
        Assert.False(Directory.Exists(Path.Combine(tempRoot.Path, "api", ".vercel")));
        Assert.Equal(linkedProjectJson, File.ReadAllText(Path.Combine(sourceRoot.Path, ".vercel", "project.json")));
        Assert.True(File.Exists(Path.Combine(sourceRoot.Path, ".vercel", "cache.json")));
    }

    [Fact]
    public async Task DeployAsyncDoesNotCopySourceRoot()
    {
        using var sourceRoot = TemporaryDirectory.Create("uncopied-source-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".dockerignore"), "node_modules");
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, ".git"));
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".git", "config"), "ignored");
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, "node_modules", "pkg"));
        File.WriteAllText(Path.Combine(sourceRoot.Path, "node_modules", "pkg", "index.js"), "ignored");
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, ".vercel"));
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".vercel", "cache.json"), "{}");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://ignored-staging-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        string tempResourceRoot = Path.Combine(tempRoot.Path, "api");
        AssertGeneratedBuildOutput(Path.Combine(tempResourceRoot, "vercel-build-output"));
        Assert.False(File.Exists(Path.Combine(tempResourceRoot, "vercel.json")));
        Assert.False(File.Exists(Path.Combine(tempResourceRoot, "Dockerfile")));
        Assert.False(File.Exists(Path.Combine(tempResourceRoot, ".dockerignore")));
        Assert.False(Directory.Exists(Path.Combine(tempResourceRoot, ".git")));
        Assert.False(Directory.Exists(Path.Combine(tempResourceRoot, "node_modules")));
        Assert.False(Directory.Exists(Path.Combine(tempResourceRoot, ".vercel")));
        Assert.True(Directory.Exists(Path.Combine(tempResourceRoot, "vercel-build-output", ".vercel", "output")));
    }

    [Fact]
    public async Task DeployAsyncAllowsSourceRootSymbolicLinks()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var sourceRoot = TemporaryDirectory.Create();
        using var outsideRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(outsideRoot.Path, "outside.txt"), "outside");
        File.CreateSymbolicLink(Path.Combine(sourceRoot.Path, "outside-link.txt"), Path.Combine(outsideRoot.Path, "outside.txt"));
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://symlink.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        Assert.Contains(runner.Invocations, invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("imagetools"));
    }

    [Fact]
    public async Task DeployAsyncThrowsWhenVercelJsonAlreadyConfiguresServices()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "vercel.json"), """{"services":{"api":{"root":"."}}}""");
        var runner = new FakeVercelCliRunner();
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("top-level 'services'", exception.Message);
        Assert.Empty(runner.Invocations);
    }

    [Theory]
    [InlineData("build", "{\"build\":{\"env\":{\"SECRET\":\"value\"}}}")]
    [InlineData("builds", "{\"builds\":[]}")]
    [InlineData("env", "{\"env\":{\"SECRET\":\"value\"}}")]
    [InlineData("routes", "{\"routes\":[]}")]
    public async Task DeployAsyncThrowsWhenVercelJsonContainsUnsupportedServicesModeTopLevelKey(string key, string vercelJson)
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "vercel.json"), vercelJson);
        var runner = new FakeVercelCliRunner();
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains($"top-level '{key}'", exception.Message);
        Assert.Empty(runner.Invocations);
    }

    [Fact]
    public async Task DeployAsyncAllowsGeneratedDockerfile()
    {
        using var sourceRoot = TemporaryDirectory.Create("generated-vercel-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "server.mjs"), "console.log('hello');");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://generated.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        var api = builder.AddContainer("api", "api")
            .WithDockerfileFactory(sourceRoot.Path, _ => Task.FromResult("""
                FROM node:22-alpine
                WORKDIR /app
                COPY server.mjs .
                CMD ["node", "server.mjs"]
                """));
        Assert.True(api.Resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile));
        dockerfile.BuildContextIgnoreContent = "node_modules\n.env*\n";

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        Assert.Contains(runner.Invocations, invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("imagetools"));
        Assert.False(File.Exists(Path.Combine(sourceRoot.Path, "Dockerfile")));
        Assert.False(File.Exists(Path.Combine(sourceRoot.Path, "Dockerfile.dockerignore")));
        Assert.False(File.Exists(Path.Combine(sourceRoot.Path, "Dockerfile.generated")));
        Assert.False(File.Exists(Path.Combine(sourceRoot.Path, "Dockerfile.generated.dockerignore")));
        Assert.NotEmpty(stateManager.SavedSections);
    }

    [Fact]
    public async Task DeployAsyncAllowsCustomDockerfileName()
    {
        using var sourceRoot = TemporaryDirectory.Create("custom-vercel-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.custom"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "index.html"), "hello");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://custom.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.custom");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        Assert.Contains(runner.Invocations, invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("imagetools"));
        Assert.NotEmpty(stateManager.SavedSections);
    }

    [Fact]
    public async Task DeployAsyncThrowsWhenVercelCliFails()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "54.18.6", ""),
            new VercelCliResult(0, "test-user", ""),
            new VercelCliResult(1, "ignored stdout", "deploy failed"));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(new FakeDeploymentStateManager());
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("Failed to deploy prebuilt resource 'api' to Vercel using 'vercel' (exit code 1). deploy failed", exception.Message);
    }

    [Fact]
    public async Task DeployAsyncThrowsWhenVercelDeployOutputHasNoDeploymentUrl()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, """{"deployment":{"id":"dpl_no_url"}}""", ""));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("did not contain an HTTP or HTTPS deployment URL", exception.Message);
        Assert.Equal(10, runner.Invocations.Count);
        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
        var deployment = Assert.Single(state.Deployments);
        Assert.Null(deployment.DeploymentUrl);
    }

    [Fact]
    public async Task DeployAsyncThrowsWhenVercelInspectFails()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new(0, "https://api.vercel.app", ""),
            new(1, "", "inspect failed"));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("Failed to verify Vercel deployment for resource 'api' using 'vercel' (exit code 1). inspect failed", exception.Message);
        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
        var deployment = Assert.Single(state.Deployments);
        Assert.Null(deployment.DeploymentUrl);
    }

    [Fact]
    public async Task DeployAsyncThrowsWhenVercelInspectOmitsReadyState()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new(0, "https://api.vercel.app", ""),
            new(0, """{"deployment":{"url":"https://api.vercel.app"}}""", ""));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("did not include a deployment ready state", exception.Message);
        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
        var deployment = Assert.Single(state.Deployments);
        Assert.Null(deployment.DeploymentUrl);
    }

    [Fact]
    public async Task DeployAsyncThrowsWhenVercelInspectReportsNonReadyState()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(
            new(0, "https://api.vercel.app", ""),
            new(0, """{"readyState":"ERROR"}""", ""));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("finished with state 'ERROR' instead of 'READY'", exception.Message);
        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
        var deployment = Assert.Single(state.Deployments);
        Assert.Null(deployment.DeploymentUrl);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesRunsVersionAndWhoami()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "Vercel CLI 54.18.6", ""),
            new(0, "davidfowl-6717", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment);

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["--version"], invocation.Arguments),
            invocation => Assert.Equal(["whoami"], invocation.Arguments));
    }

    [Fact]
    public async Task ValidateCliPrerequisitesValidatesConfiguredScope()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(0, "project-list", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.AddVercelEnvironment("vercel")
            .WithVercelScope("team");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment);

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["--version"], invocation.Arguments),
            invocation => Assert.Equal(["whoami"], invocation.Arguments),
            invocation => Assert.Equal(["project", "ls", "--scope", "team", "--format=json"], invocation.Arguments));
    }

    [Fact]
    public async Task ValidateCliPrerequisitesThrowsWhenScopeValidationFails()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(1, "", "scope not found"));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.AddVercelEnvironment("vercel")
            .WithVercelScope("missing-team");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment));

        Assert.Contains("Failed to validate Vercel scope 'missing-team' using 'vercel' (exit code 1). scope not found", exception.Message);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesThrowsWhenWhoamiFails()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(1, "", "not logged in"));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment));

        Assert.Contains("Failed to validate Vercel authentication using 'vercel' (exit code 1). not logged in", exception.Message);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesThrowsWhenVersionFailsWithStandardOutput()
    {
        var runner = new FakeVercelCliRunner(new VercelCliResult(1, "missing vercel", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment));

        Assert.Contains("Failed to validate Vercel CLI installation using 'vercel' (exit code 1). missing vercel", exception.Message);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesThrowsWhenVersionIsTooOld()
    {
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, "54.18.5", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment));

        Assert.Contains("Vercel CLI version '54.18.5' is not supported", exception.Message);
        Assert.Single(runner.Invocations);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesThrowsWhenVersionCannotBeParsed()
    {
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, "vercel dev build", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment));

        Assert.Contains("Failed to determine Vercel CLI version", exception.Message);
        Assert.Single(runner.Invocations);
    }

    [Fact]
    public async Task ValidatePrerequisitesThrowsWhenNoResourcesArePublished()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        var vercel = builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidatePrerequisitesAsync(context, environment));

        Assert.Contains("No image-build compute resources target Vercel", exception.Message);
    }

    [Fact]
    public async Task DestroyAsyncDeletesProjectsFromSavedDeploymentState()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(0, ProjectListJson("a-project", "z-project"), ""),
            new(0, ProjectListJson("a-project"), ""),
            new(0, "", ""),
            new(0, ProjectListJson("z-project"), ""),
            new(0, "", ""));
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            "team",
            null,
            false,
            [
                new("api", "z-project", "prj_z", "dpl_1", "https://z-project.vercel.app", "/src/api", true),
                new("worker", "a-project", null, null, null, "/src/worker", true),
                new("api2", "z-project", null, null, null, "/src/api2", true)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelScope("team");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["--version"], invocation.Arguments),
            invocation => Assert.Equal(["whoami"], invocation.Arguments),
            invocation => Assert.Equal(["project", "ls", "--scope", "team", "--format=json"], invocation.Arguments),
            invocation => Assert.Equal(["project", "ls", "--scope", "team", "--filter", "a-project", "--format=json"], invocation.Arguments),
            invocation =>
            {
                Assert.Equal(["project", "remove", "a-project", "--scope", "team"], invocation.Arguments);
                Assert.Equal("y\n", invocation.StandardInput);
            },
            invocation => Assert.Equal(["project", "ls", "--scope", "team", "--filter", "z-project", "--format=json"], invocation.Arguments),
            invocation =>
            {
                Assert.Equal(["project", "remove", "z-project", "--scope", "team"], invocation.Arguments);
                Assert.Equal("y\n", invocation.StandardInput);
            });
        Assert.Single(stateManager.DeletedSections);
    }

    [Fact]
    public async Task DestroyAsyncDoesNotFallBackToConfiguredDeploymentsWhenStateIsMissing()
    {
        using var sourceRoot = TemporaryDirectory.Create("fallback-project");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner();
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        Assert.Empty(runner.Invocations);
        Assert.Empty(stateManager.DeletedSections);
        var summary = Assert.Single(context.Summary.Items);
        Assert.Equal("Vercel destroy", summary.Key);
        Assert.Contains("No Vercel deployment state", summary.Value);
    }

    [Fact]
    public async Task DestroyAsyncAddsSummaryWhenNoStateExists()
    {
        var runner = new FakeVercelCliRunner();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(new FakeDeploymentStateManager());
        var vercel = builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        Assert.Empty(runner.Invocations);
        var summary = Assert.Single(context.Summary.Items);
        Assert.Equal("Vercel destroy", summary.Key);
        Assert.Contains("No Vercel deployment state", summary.Value);
    }

    [Fact]
    public async Task DestroyAsyncRejectsDeploymentStateFromDifferentScope()
    {
        var runner = new FakeVercelCliRunner();
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            "team-a",
            null,
            false,
            [
                new("api", "api-project", "prj_1", "dpl_1", "https://api-project.vercel.app", "/src/api", true)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel")
            .WithVercelScope("team-b");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DestroyAsync(context, environment));

        Assert.Contains("created for scope 'team-a'", exception.Message);
        Assert.Contains("configured for scope 'team-b'", exception.Message);
        Assert.Empty(runner.Invocations);
        Assert.Empty(stateManager.DeletedSections);
    }

    [Fact]
    public async Task DestroyAsyncSkipsUnmanagedProjectsAndClearsState()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(0, ProjectListJson("managed-project"), ""),
            new(0, "", ""));
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            null,
            null,
            false,
            [
                new("api", "managed-project", "prj_managed", "dpl_1", "https://managed-project.vercel.app", "/src/api", true),
                new("docs", "preexisting-project", "prj_existing", "dpl_2", "https://preexisting-project.vercel.app", "/src/docs", false)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["--version"], invocation.Arguments),
            invocation => Assert.Equal(["whoami"], invocation.Arguments),
            invocation => Assert.Equal(["project", "ls", "--filter", "managed-project", "--format=json"], invocation.Arguments),
            invocation => Assert.Equal(["project", "remove", "managed-project"], invocation.Arguments));
        Assert.Single(stateManager.DeletedSections);
    }

    [Fact]
    public async Task DestroyAsyncSkipsCliWhenStateHasOnlyUnmanagedProjects()
    {
        var runner = new FakeVercelCliRunner(new VercelCliResult(1, "", "not logged in"));
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            null,
            null,
            false,
            [
                new("docs", "preexisting-project", "prj_existing", "dpl_2", "https://preexisting-project.vercel.app", "/src/docs", false)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        Assert.Empty(runner.Invocations);
        Assert.Single(stateManager.DeletedSections);
    }

    [Fact]
    public async Task DestroyAsyncRemovesTrackedEnvironmentVariablesFromUnmanagedProjects()
    {
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        var runner = new FakeVercelCliRunner();
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            null,
            null,
            false,
            [
                new("docs", "preexisting-project", "prj_existing", "dpl_2", "https://preexisting-project.vercel.app", "/src/docs", false)
                {
                    ProjectEnvironmentVariables = ["API_KEY"]
                }
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        string expectedProjectLinkDirectory = Path.Combine(tempRoot.Path, "vercel", ".vercel-projects", "preexisting-project");
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(["link", "--cwd", expectedProjectLinkDirectory, "--yes", "--project", "prj_existing"]));
        Assert.Contains(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(["env", "rm", "API_KEY", "preview", "--cwd", expectedProjectLinkDirectory, "--yes"]));
        Assert.Single(stateManager.DeletedSections);
    }

    [Fact]
    public async Task DestroyAsyncTreatsMissingManagedProjectAsConverged()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(0, ProjectListJson(), ""));
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            null,
            null,
            false,
            [
                new("api", "managed-project", "prj_managed", "dpl_1", "https://managed-project.vercel.app", "/src/api", true)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["--version"], invocation.Arguments),
            invocation => Assert.Equal(["whoami"], invocation.Arguments),
            invocation => Assert.Equal(["project", "ls", "--filter", "managed-project", "--format=json"], invocation.Arguments));
        var summary = Assert.Single(context.Summary.Items, item => item.Key == "Vercel project already absent");
        Assert.Equal("managed-project", summary.Value);
        Assert.Single(stateManager.SavedSections);
        Assert.Single(stateManager.DeletedSections);
    }

    [Fact]
    public async Task DestroyAsyncPreservesStateWhenProjectRemovalFails()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(0, ProjectListJson("a-project"), ""),
            new(0, "", ""),
            new(0, ProjectListJson("b-project"), ""),
            new(1, "", "remove failed"),
            new(0, ProjectListJson("b-project"), ""));
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            1,
            "vercel",
            null,
            null,
            false,
            [
                new("api", "a-project", "prj_a", "dpl_a", "https://a-project.vercel.app", "/src/api", true),
                new("worker", "b-project", "prj_b", "dpl_b", "https://b-project.vercel.app", "/src/worker", true)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IVercelContainerRegistryClient>(new FakeVercelContainerRegistryClient());
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DestroyAsync(context, environment));

        Assert.Contains("destroy Vercel project 'b-project'", exception.Message);
        Assert.Equal(7, runner.Invocations.Count);
        Assert.Empty(stateManager.DeletedSections);
        var savedSection = Assert.Single(stateManager.SavedSections);
        string savedState = savedSection.Data.First().Value!.GetValue<string>();
        Assert.DoesNotContain("\"projectName\": \"a-project\"", savedState);
        Assert.Contains("\"projectName\": \"b-project\"", savedState);
    }

    [Fact]
    public async Task VercelCliRunnerRunsProcessAndCapturesOutput()
    {
        var runner = new VercelCliRunner();

        var result = await runner.RunAsync("dotnet", ["--version"], Directory.GetCurrentDirectory(), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
    }

    [Fact]
    public async Task VercelCliRunnerThrowsWhenProcessCannotStart()
    {
        var runner = new VercelCliRunner();

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            runner.RunAsync($"missing-vercel-cli-{Guid.NewGuid():N}", [], workingDirectory: null, TestContext.Current.CancellationToken));

        Assert.Contains("could not be started", exception.Message);
    }

    [Fact]
    public void GetDeploymentUrlReturnsPlainUrl()
    {
        string url = VercelCliOutputParser.GetDeploymentUrl("""
            Vercel CLI 54.18.6
            https://example.vercel.app
            """);

        Assert.Equal("https://example.vercel.app", url);
    }

    [Fact]
    public void GetDeploymentUrlReturnsJsonDeploymentUrl()
    {
        string url = VercelCliOutputParser.GetDeploymentUrl("""
            {
              "status": "ok",
              "deployment": {
                "id": "dpl_123",
                "url": "https://example-json.vercel.app",
                "readyState": "READY"
              }
            }
            """);

        Assert.Equal("https://example-json.vercel.app", url);
    }

    [Fact]
    public void GetDeploymentResultReturnsRootJsonDeploymentUrl()
    {
        var result = VercelCliOutputParser.GetDeploymentResult("""
            {
              "id": "dpl_root",
              "url": "https://root-json.vercel.app"
            }
            """);

        Assert.Equal("dpl_root", result.DeploymentId);
        Assert.Equal("https://root-json.vercel.app", result.DeploymentUrl);
    }

    [Fact]
    public void GetDeploymentResultFallsBackWhenJsonIsInvalid()
    {
        var result = VercelCliOutputParser.GetDeploymentResult("""
            {
              "deployment":
            }
            https://fallback.vercel.app
            """);

        Assert.Null(result.DeploymentId);
        Assert.Equal("https://fallback.vercel.app", result.DeploymentUrl);
    }

    [Fact]
    public void GetDeploymentResultThrowsWhenOutputHasNoUrl()
    {
        var exception = Assert.Throws<DistributedApplicationException>(() =>
            VercelCliOutputParser.GetDeploymentResult("""
            {
              "deployment": {
                "id": "dpl_no_url"
              }
            }
            """));

        Assert.Contains("did not contain an HTTP or HTTPS deployment URL", exception.Message);
    }

    [Fact]
    public void GetDeploymentResultReturnsJsonDeploymentId()
    {
        var result = VercelCliOutputParser.GetDeploymentResult("""
            {
              "status": "ok",
              "deployment": {
                "id": "dpl_123",
                "url": "https://example-json.vercel.app",
                "readyState": "READY"
              }
            }
            """);

        Assert.Equal("dpl_123", result.DeploymentId);
        Assert.Equal("https://example-json.vercel.app", result.DeploymentUrl);
    }

    [Fact]
    public void GetDeploymentInspectionParsesObservedJsonShapes()
    {
        Assert.Equal("READY", VercelCliOutputParser.GetDeploymentInspection("""{ "readyState": "READY" }""").ReadyState);
        Assert.Equal("READY", VercelCliOutputParser.GetDeploymentInspection("""{ "state": "READY" }""").ReadyState);
        Assert.Equal("READY", VercelCliOutputParser.GetDeploymentInspection("""{ "deployment": { "readyState": "READY" } }""").ReadyState);
        Assert.Equal("READY", VercelCliOutputParser.GetDeploymentInspection("""{ "deployment": { "state": "READY" } }""").ReadyState);
    }

    [Fact]
    public void GetDeploymentInspectionThrowsForInvalidJson()
    {
        var exception = Assert.Throws<DistributedApplicationException>(() =>
            VercelCliOutputParser.GetDeploymentInspection("""{ "deployment": """));

        Assert.Contains("vercel inspect", exception.Message);
    }

    [Fact]
    public void GetVercelProjectNameReadsVercelProjectLink()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string vercelDirectory = Path.Combine(sourceRoot.Path, ".vercel");
        Directory.CreateDirectory(vercelDirectory);
        File.WriteAllText(Path.Combine(vercelDirectory, "project.json"), """{"projectName":"linked-project"}""");
        var entry = CreateDeploymentEntry(sourceRoot.Path);
        entry.Resource.Annotations.Add(new VercelProjectOptionsAnnotation("configured-project"));

        string projectName = VercelProjectNameResolver.GetProjectName(entry);

        Assert.Equal("linked-project", projectName);
    }

    [Fact]
    public void GetVercelProjectNameFallsBackToSourceRootName()
    {
        using var sourceRoot = TemporaryDirectory.Create("fallback-project");
        var entry = CreateDeploymentEntry(sourceRoot.Path);

        string projectName = VercelProjectNameResolver.GetProjectName(entry);

        Assert.Equal("fallback-project", projectName);
    }

    [Fact]
    public void GetVercelOptionsReturnsDefaultOptionsWithoutAnnotation()
    {
        var resource = new VercelEnvironmentResource("vercel");

        var options = resource.GetVercelOptions();

        Assert.Null(options.Scope);
        Assert.Null(options.Target);
        Assert.False(options.Production);
    }

    private static PipelineStepContext CreatePipelineStepContext(
        IDistributedApplicationBuilder builder,
        DistributedApplication app,
        ILogger? logger = null)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var pipelineContext = new PipelineContext(
            model,
            builder.ExecutionContext,
            app.Services,
            logger ?? NullLogger.Instance,
            TestContext.Current.CancellationToken);

        return new()
        {
            PipelineContext = pipelineContext,
            ReportingStep = NoopReportingStep.Instance
        };
    }

    private static async Task<List<PipelineStep>> CreateConfiguredPipelineStepsAsync(
        IDistributedApplicationBuilder builder,
        DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var stepContext = CreatePipelineStepContext(builder, app);
        List<PipelineStep> steps = [];

        foreach (var resource in model.Resources)
        {
            foreach (var annotation in resource.Annotations.OfType<PipelineStepAnnotation>())
            {
                var createdSteps = await annotation.CreateStepsAsync(new()
                {
                    PipelineContext = stepContext.PipelineContext,
                    Resource = resource
                });
                steps.AddRange(createdSteps);
            }
        }

        if (!steps.Any(static step => step.Name == "push-prereq"))
        {
            steps.Add(new PipelineStep
            {
                Name = "push-prereq",
                Action = _ => Task.CompletedTask
            });
        }

        foreach (var resource in model.Resources)
        {
            foreach (var annotation in resource.Annotations.OfType<PipelineConfigurationAnnotation>())
            {
                await annotation.Callback(new()
                {
                    Model = model,
                    Services = app.Services,
                    Steps = steps
                });
            }
        }

        return steps;
    }

    private static void AssertPipelineDependenciesAreDistinct(IEnumerable<PipelineStep> steps)
    {
        foreach (var step in steps)
        {
            Assert.Equal(step.DependsOnSteps.Distinct(StringComparer.Ordinal), step.DependsOnSteps);
            Assert.Equal(step.RequiredBySteps.Distinct(StringComparer.Ordinal), step.RequiredBySteps);
        }
    }

    private static VercelDeploymentEntry CreateDeploymentEntry(string sourceRoot)
    {
        string dockerfilePath = Path.Combine(sourceRoot, "Dockerfile");
        return new(
            new ContainerResource("api"),
            sourceRoot,
            dockerfilePath,
            new DockerfileBuildAnnotation(sourceRoot, dockerfilePath, stage: null));
    }

    private static VercelPreparedDeploymentAnnotation CreatePreparedDeployment(
        IResource resource,
        string oidcToken,
        string projectName)
    {
        var entry = new VercelDeploymentEntry(resource, Path.GetTempPath());
        var project = new VercelPulledProject(
            projectName,
            $"prj_{projectName}",
            $"team_{projectName}",
            "{}",
            oidcToken);
        var claims = new VercelOidcClaims(
            $"team_{projectName}",
            "test-team",
            projectName,
            $"prj_{projectName}");
        var projectContext = new VercelPulledProjectContext(
            VercelEnvironmentConfiguration.Empty,
            project,
            claims);

        return new(
            entry,
            new(projectName, project.ProjectId),
            projectContext,
            ManagedByAspire: true,
            RemoteImageName: "app",
            RemoteImageTag: "aspire-test",
            TaggedImageReference: $"vcr.vercel.com/test-team/{projectName}/app:aspire-test");
    }

    private static void WriteVercelProjectLink(string sourceRoot, string projectName, string projectId)
    {
        string vercelDirectory = Path.Combine(sourceRoot, ".vercel");
        Directory.CreateDirectory(vercelDirectory);
        File.WriteAllText(Path.Combine(vercelDirectory, "project.json"), $$"""
            {
              "projectId": "{{projectId}}",
              "projectName": "{{projectName}}"
            }
            """);
    }

    private static VercelCliResult ReadyInspectResult()
        => new(0, """{"readyState":"READY"}""", "");

    private static string ProjectListJson(params string[] projectNames)
        => JsonSerializer.Serialize(new
        {
            projects = projectNames.Select(static projectName => new
            {
                name = projectName,
                id = $"prj_{projectName.Replace('-', '_')}"
            })
        });

    private static string EnvListJson(params string[] names)
        => JsonSerializer.Serialize(new
        {
            envs = names.Select(static name => new
            {
                key = name,
                target = new[] { "preview" }
            })
        });

    private static async Task<DistributedApplicationException> AssertWriteDeploymentPlanThrowsAsync(
        Action<IResourceBuilder<ContainerResource>> configureApi,
        string? directoryName = null)
        => await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            WriteDeploymentPlanForConfiguredContainerAsync(configureApi, directoryName));

    private static async Task AssertWriteDeploymentPlanSucceedsAsync(
        Action<IResourceBuilder<ContainerResource>> configureApi,
        string? directoryName = null)
        => await WriteDeploymentPlanForConfiguredContainerAsync(configureApi, directoryName);

    private static async Task WriteDeploymentPlanForConfiguredContainerAsync(
        Action<IResourceBuilder<ContainerResource>> configureApi,
        string? directoryName = null)
    {
        using var sourceRoot = TemporaryDirectory.Create(directoryName);
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        var api = builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");
        configureApi(api);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        await VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken);
    }

    private static VercelDeploymentState ReadSavedState(DeploymentStateSection section)
        => JsonSerializer.Deserialize<VercelDeploymentState>(
            section.Data.First().Value!.GetValue<string>(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static string CreateTestJwt(string payloadJson)
        => $"{Base64Url("{}")}.{Base64Url(payloadJson)}.signature";

    private static string Base64Url(string value)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    [Fact]
    public async Task PreparePulledProjectContextDeletesScratchLinkDirectory()
    {
        using var sourceRoot = TemporaryDirectory.Create("pulled-project-context");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel")
            .WithVercelProductionDeployments();
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment));
        var entriesByResourceName = VercelDeploymentModel.GetEntries(model, environment)
            .ToDictionary(static deployment => deployment.Resource.Name, StringComparer.Ordinal);

        var projectContext = await VercelDeploymentStep.PreparePulledProjectContextAsync(
            context,
            runner,
            environment.GetVercelOptions(),
            entry,
            entriesByResourceName);

        string expectedProjectLinkDirectory = Path.Combine(tempRoot.Path, "api", ".vercel-project");
        string expectedProjectName = VercelProjectNameResolver.GetProjectName(entry);
        Assert.False(Directory.Exists(expectedProjectLinkDirectory));
        Assert.Equal(expectedProjectName, projectContext.PulledProject.ProjectName);
        Assert.Equal("prj_test", projectContext.PulledProject.ProjectId);
        Assert.Equal("team_test", projectContext.OidcClaims.OwnerId);
        Assert.Equal("test-team", projectContext.OidcClaims.Owner);
        Assert.Equal("test-project", projectContext.OidcClaims.Project);
        Assert.Contains("\"projectId\": \"prj_test\"", projectContext.PulledProject.ProjectJsonContent, StringComparison.Ordinal);
        Assert.Empty(projectContext.EnvironmentConfiguration.DeploymentEnvironmentVariables);
        Assert.Empty(projectContext.EnvironmentConfiguration.ProjectEnvironmentVariables);

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["link", "--cwd", expectedProjectLinkDirectory, "--yes", "--project", expectedProjectName], invocation.Arguments),
            invocation => Assert.Equal(["pull", "--cwd", expectedProjectLinkDirectory, "--yes", "--environment", "production"], invocation.Arguments));
    }

    [Fact]
    public async Task WriteBuildOutputAsyncWritesProviderArtifactShape()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var deployRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentModel.GetEntries(model, environment)) with
        {
            DeployDirectory = deployRoot.Path
        };
        var project = new VercelPulledProject(
            "api",
            "prj_test",
            "team_test",
            """
            {
              "projectId": "prj_test",
              "orgId": "team_test",
              "projectName": "api"
            }
            """,
            OidcToken: "unused");

        await VercelBuildOutputWriter.WriteAsync(
            entry,
            project,
            $"vcr.vercel.com/test-team/test-project/app@{FakeVercelCliRunner.FakeImageDigest}",
            TestContext.Current.CancellationToken);

        AssertGeneratedBuildOutput(deployRoot.Path, expectedProjectName: "api");
    }

    private static void AssertGeneratedBuildOutput(string deployDirectory, string? expectedProjectName = null)
    {
        string outputDirectory = Path.Combine(deployDirectory, ".vercel", "output");
        string projectJsonPath = Path.Combine(deployDirectory, ".vercel", "project.json");
        string configJsonPath = Path.Combine(outputDirectory, "config.json");
        string functionConfigPath = Path.Combine(outputDirectory, "functions", "index.func", ".vc-config.json");
        var generatedFiles = Directory.EnumerateFiles(deployDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(deployDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                ".vercel/output/config.json",
                ".vercel/output/functions/index.func/.vc-config.json",
                ".vercel/project.json"
            ],
            generatedFiles);

        using var projectDocument = JsonDocument.Parse(File.ReadAllText(projectJsonPath));
        var project = projectDocument.RootElement;
        Assert.Equal("prj_test", project.GetProperty("projectId").GetString());
        Assert.Equal("team_test", project.GetProperty("orgId").GetString());
        string? projectName = project.GetProperty("projectName").GetString();
        if (expectedProjectName is null)
        {
            Assert.False(string.IsNullOrWhiteSpace(projectName));
        }
        else
        {
            Assert.Equal(expectedProjectName, projectName);
        }

        using var configDocument = JsonDocument.Parse(File.ReadAllText(configJsonPath));
        var root = configDocument.RootElement;
        Assert.Equal(["version", "routes"], root.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal(3, root.GetProperty("version").GetInt32());
        var routes = root.GetProperty("routes").EnumerateArray().ToArray();
        Assert.Equal(2, routes.Length);
        Assert.Equal(["handle"], routes[0].EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal("filesystem", routes[0].GetProperty("handle").GetString());
        Assert.Equal(["src", "dest"], routes[1].EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal("/(.*)", routes[1].GetProperty("src").GetString());
        Assert.Equal("/index", routes[1].GetProperty("dest").GetString());

        using var functionDocument = JsonDocument.Parse(File.ReadAllText(functionConfigPath));
        var functionConfig = functionDocument.RootElement;
        Assert.Equal(["handler", "runtime", "environment"], functionConfig.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal("container", functionConfig.GetProperty("runtime").GetString());
        Assert.Equal($"vcr.vercel.com/test-team/test-project/app@{FakeVercelCliRunner.FakeImageDigest}", functionConfig.GetProperty("handler").GetString());
        Assert.Empty(functionConfig.GetProperty("environment").EnumerateObject());
    }

    private sealed class FakeProjectMetadata(string projectPath) : IProjectMetadata
    {
        public bool IsFileBasedApp => false;

        public LaunchSettings? LaunchSettings => null;

        public string ProjectPath => projectPath;

        public bool SuppressBuild => false;
    }

    private sealed class TestLanguageAppResource(string name, string command, string workingDirectory)
        : ExecutableResource(name, command, workingDirectory);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create(string? directoryName = null)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), directoryName ?? $"vercel-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);

            return new(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FakeVercelCliRunner(params VercelCliResult[] results) : IVercelCliRunner
    {
        private readonly Queue<VercelCliResult> _results = new(results);
        private readonly HashSet<string> _projects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _linkedProjects = new(StringComparer.Ordinal);
        private readonly HashSet<string> _projectEnvironmentVariables = new(StringComparer.Ordinal)
        {
            "API_KEY",
            "AUTH_HEADER",
            "OLD_KEY"
        };
        public const string FakeImageDigest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public const string FakeImageIndexDigest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        public const string FakeImageManifestJson = """
            {
              "schemaVersion": 2,
              "mediaType": "application/vnd.oci.image.index.v1+json",
              "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
              "manifests": [
                {
                  "mediaType": "application/vnd.oci.image.manifest.v1+json",
                  "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "platform": {
                    "architecture": "amd64",
                    "os": "linux"
                  }
                },
                {
                  "mediaType": "application/vnd.oci.image.manifest.v1+json",
                  "digest": "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                  "platform": {
                    "architecture": "unknown",
                    "os": "unknown"
                  }
                }
              ]
            }
            """;

        public List<VercelCliInvocation> Invocations { get; } = [];

        public Task<VercelCliResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory,
            CancellationToken cancellationToken,
            string? standardInput = null)
        {
            Invocations.Add(new(fileName, [.. arguments], workingDirectory, standardInput));

            if (TryGetAutoResult(fileName, arguments, workingDirectory, out var autoResult))
            {
                return Task.FromResult(autoResult);
            }

            var result = _results.Count > 0
                ? _results.Dequeue()
                : new VercelCliResult(0, "", "");

            return Task.FromResult(result);
        }

        private bool TryGetAutoResult(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory,
            out VercelCliResult result)
        {
            result = new VercelCliResult(0, "", "");

            if (string.Equals(fileName, "docker", StringComparison.Ordinal))
            {
                if (arguments.Count >= 2
                    && string.Equals(arguments[0], "buildx", StringComparison.Ordinal)
                    && string.Equals(arguments[1], "version", StringComparison.Ordinal))
                {
                    return true;
                }

                if (arguments.Count >= 2
                    && string.Equals(arguments[0], "buildx", StringComparison.Ordinal)
                    && string.Equals(arguments[1], "imagetools", StringComparison.Ordinal))
                {
                    result = new VercelCliResult(0, FakeImageManifestJson, "");
                    return true;
                }

                if (arguments.Count >= 1
                    && string.Equals(arguments[0], "login", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            if (string.Equals(fileName, "vercel", StringComparison.Ordinal))
            {
                if (arguments is ["--version"] && ShouldAutoVercelVersion())
                {
                    result = new VercelCliResult(0, "54.18.6", "");
                    return true;
                }

                if (arguments is ["whoami"] && ShouldAutoVercelWhoami())
                {
                    result = new VercelCliResult(0, "test-user", "");
                    return true;
                }

                if (arguments.Count >= 2
                    && string.Equals(arguments[0], "project", StringComparison.Ordinal)
                    && string.Equals(arguments[1], "ls", StringComparison.Ordinal)
                    && ShouldAutoVercelProjectList())
                {
                    string? filter = GetOptionValue(arguments, "--filter");
                    string[] projects = _projects
                        .Where(project => string.IsNullOrWhiteSpace(filter) || project.Contains(filter, StringComparison.Ordinal))
                        .Order(StringComparer.Ordinal)
                        .ToArray();
                    result = new VercelCliResult(0, ProjectListJson(projects), "");
                    return true;
                }
            }

            for (int i = 0; i < arguments.Count - 1; i++)
            {
                if (string.Equals(arguments[i], "project", StringComparison.Ordinal)
                    && string.Equals(arguments[i + 1], "add", StringComparison.Ordinal))
                {
                    if (i + 2 < arguments.Count)
                    {
                        _projects.Add(arguments[i + 2]);
                    }

                    return true;
                }
            }

            if (arguments.Count >= 3
                && string.Equals(arguments[0], "project", StringComparison.Ordinal)
                && string.Equals(arguments[1], "remove", StringComparison.Ordinal)
                && ShouldAutoVercelProjectRemove())
            {
                _projects.Remove(arguments[2]);
                return true;
            }

            if (arguments.Count > 0
                && string.Equals(arguments[0], "link", StringComparison.Ordinal))
            {
                string? cwd = GetOptionValue(arguments, "--cwd") ?? workingDirectory;
                string? project = GetOptionValue(arguments, "--project");
                if (cwd is not null && project is not null)
                {
                    _linkedProjects[cwd] = project;
                }

                return true;
            }

            if (arguments.Count >= 2
                && string.Equals(arguments[0], "env", StringComparison.Ordinal)
                && string.Equals(arguments[1], "add", StringComparison.Ordinal))
            {
                if (arguments.Count >= 3)
                {
                    _projectEnvironmentVariables.Add(arguments[2]);
                }

                return true;
            }

            if (arguments.Count >= 2
                && string.Equals(arguments[0], "env", StringComparison.Ordinal)
                && string.Equals(arguments[1], "ls", StringComparison.Ordinal))
            {
                result = new VercelCliResult(0, EnvListJson([.. _projectEnvironmentVariables.Order(StringComparer.Ordinal)]), "");
                return true;
            }

            if (arguments.Count >= 2
                && string.Equals(arguments[0], "env", StringComparison.Ordinal)
                && string.Equals(arguments[1], "rm", StringComparison.Ordinal))
            {
                if (arguments.Count >= 3)
                {
                    _projectEnvironmentVariables.Remove(arguments[2]);
                }

                return true;
            }

            if (arguments.Count > 0
                && string.Equals(arguments[0], "pull", StringComparison.Ordinal))
            {
                string? cwd = GetOptionValue(arguments, "--cwd") ?? workingDirectory;
                string targetEnvironment = GetOptionValue(arguments, "--environment") ?? "preview";
                if (cwd is not null)
                {
                    WritePulledProjectFiles(cwd, targetEnvironment);
                }

                return true;
            }

            return false;
        }

        private bool ShouldAutoVercelVersion()
        {
            if (!_results.TryPeek(out var next))
            {
                return true;
            }

            string output = $"{next.StandardOutput}{Environment.NewLine}{next.StandardError}";
            if (LooksLikeDeployResult(output))
            {
                return true;
            }

            return next.Succeeded
                && !VercelCliOutputParser.TryGetCliVersion(output, out _)
                && !output.TrimStart().StartsWith("vercel ", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldAutoVercelWhoami()
            => !_results.TryPeek(out var next)
                || (next.Succeeded && (IsEmptyResult(next) || LooksLikeDeployResult(next.StandardOutput)));

        private bool ShouldAutoVercelProjectList()
            => !_results.TryPeek(out var next)
                || (next.Succeeded && (IsEmptyResult(next) || (LooksLikeDeployResult(next.StandardOutput) && !LooksLikeProjectListResult(next.StandardOutput))));

        private bool ShouldAutoVercelProjectRemove()
            => !_results.TryPeek(out _);

        private static bool IsEmptyResult(VercelCliResult result)
            => string.IsNullOrWhiteSpace(result.StandardOutput)
                && string.IsNullOrWhiteSpace(result.StandardError);

        private static bool LooksLikeDeployResult(string output)
            => output.Contains("https://", StringComparison.OrdinalIgnoreCase)
                || output.TrimStart().StartsWith("{", StringComparison.Ordinal);

        private static bool LooksLikeProjectListResult(string output)
        {
            string trimmed = output.TrimStart();
            return trimmed.StartsWith("[", StringComparison.Ordinal)
                || trimmed.StartsWith("{\"projects\"", StringComparison.Ordinal)
                || trimmed.StartsWith("{ \"projects\"", StringComparison.Ordinal);
        }

        private void WritePulledProjectFiles(string directory, string targetEnvironment)
        {
            string project = _linkedProjects.GetValueOrDefault(directory, "api");
            string vercelDirectory = Path.Combine(directory, ".vercel");
            Directory.CreateDirectory(vercelDirectory);
            File.WriteAllText(Path.Combine(vercelDirectory, "project.json"), $$"""
                {
                  "projectId": "prj_test",
                  "orgId": "team_test",
                  "projectName": "{{project}}",
                  "settings": {
                    "createdAt": 0,
                    "framework": null,
                    "devCommand": null,
                    "installCommand": null,
                    "buildCommand": null,
                    "outputDirectory": null,
                    "rootDirectory": null,
                    "directoryListing": false,
                    "nodeVersion": "22.x"
                  }
                }
                """);
            File.WriteAllText(Path.Combine(vercelDirectory, $".env.{targetEnvironment}.local"), $"{VercelOidcTokenEnvironmentVariableForTests()}=\"{CreateFakeOidcToken()}\"{Environment.NewLine}");
            File.WriteAllText(Path.Combine(directory, ".env.local"), $"{VercelOidcTokenEnvironmentVariableForTests()}=\"{CreateFakeOidcToken()}\"{Environment.NewLine}");
        }

        private static string? GetOptionValue(IReadOnlyList<string> arguments, string option)
        {
            for (int i = 0; i < arguments.Count - 1; i++)
            {
                if (string.Equals(arguments[i], option, StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }

        private static string CreateFakeOidcToken()
        {
            string header = Base64UrlEncode("""{"alg":"none"}""");
            string payload = Base64UrlEncode("""{"owner_id":"team_test","owner":"test-team","project":"test-project","project_id":"prj_test"}""");
            return $"{header}.{payload}.";
        }

        private static string Base64UrlEncode(string value)
            => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private static string VercelOidcTokenEnvironmentVariableForTests() => "VERCEL_OIDC_TOKEN";
    }

    private sealed class FakeVercelContainerRegistryClient : IVercelContainerRegistryClient
    {
        public List<string> Repositories { get; } = [];

        public Task EnsureRepositoryAsync(string token, VercelOidcClaims claims, string repository, CancellationToken cancellationToken)
        {
            Repositories.Add(repository);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new(logLevel, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed record VercelCliInvocation(
        string FileName,
        string[] Arguments,
        string? WorkingDirectory,
        string? StandardInput);

    private sealed class ThrowingResourceContainerImageManager : IResourceContainerImageManager
    {
        public int CallCount { get; private set; }

        public Task BuildImageAsync(IResource resource, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("Vercel deployments must not build container images locally.");
        }

        public Task BuildImagesAsync(IEnumerable<IResource> resources, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("Vercel deployments must not build container images locally.");
        }

        public Task PushImageAsync(IResource resource, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("Vercel deployments must not push container images.");
        }
    }

    private sealed class VerifyingResourceContainerImageManager(FakeVercelCliRunner runner) : IResourceContainerImageManager
    {
        private int _activePushes;

        public List<string> PushedResources { get; } = [];

        public Task BuildImageAsync(IResource resource, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task BuildImagesAsync(IEnumerable<IResource> resources, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public async Task PushImageAsync(IResource resource, CancellationToken cancellationToken)
        {
            Assert.Equal(1, Interlocked.Increment(ref _activePushes));
            try
            {
                await Task.Delay(25, cancellationToken);

                var preparedDeployment = Assert.Single(resource.Annotations.OfType<VercelPreparedDeploymentAnnotation>());
                var loginInvocation = runner.Invocations.Last(invocation => invocation.FileName == "docker" && invocation.Arguments.Contains("login"));
                Assert.Equal(preparedDeployment.ProjectContext.PulledProject.OidcToken, loginInvocation.StandardInput);
                PushedResources.Add(resource.Name);
            }
            finally
            {
                Interlocked.Decrement(ref _activePushes);
            }
        }
    }

    private sealed class FakeDeploymentStateManager : IDeploymentStateManager
    {
        private readonly Dictionary<string, DeploymentStateSection> _sections = new(StringComparer.Ordinal);

        public string? StateFilePath => null;

        public List<DeploymentStateSection> SavedSections { get; } = [];

        public List<DeploymentStateSection> DeletedSections { get; } = [];

        public void SetSection(string sectionName, string value)
        {
            var section = new DeploymentStateSection(sectionName, new JsonObject(), 0);
            section.SetValue(value);
            _sections[sectionName] = section;
        }

        public Task<DeploymentStateSection> AcquireSectionAsync(string sectionName, CancellationToken cancellationToken)
        {
            if (!_sections.TryGetValue(sectionName, out var section))
            {
                section = new(sectionName, new JsonObject(), 0);
                _sections.Add(sectionName, section);
            }

            return Task.FromResult(section);
        }

        public Task SaveSectionAsync(DeploymentStateSection section, CancellationToken cancellationToken)
        {
            SavedSections.Add(section);
            _sections[section.SectionName] = section;
            return Task.CompletedTask;
        }

        public Task DeleteSectionAsync(DeploymentStateSection section, CancellationToken cancellationToken)
        {
            DeletedSections.Add(section);
            _sections.Remove(section.SectionName);
            return Task.CompletedTask;
        }

        public Task ClearAllStateAsync(CancellationToken cancellationToken)
        {
            _sections.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class FakePipelineOutputService(string outputDirectory, string? tempDirectory = null) : IPipelineOutputService
    {
        private readonly string _tempDirectory = tempDirectory ?? outputDirectory;

        public string GetOutputDirectory() => outputDirectory;

        public string GetOutputDirectory(IResource resource) => outputDirectory;

        public string GetTempDirectory() => _tempDirectory;

        public string GetTempDirectory(IResource resource) => Path.Combine(_tempDirectory, resource.Name);
    }

    private sealed class NoopReportingStep : IReportingStep
    {
        public static NoopReportingStep Instance { get; } = new();

        public Task<IReportingTask> CreateTaskAsync(MarkdownString statusText, CancellationToken cancellationToken = default)
            => Task.FromResult<IReportingTask>(NoopReportingTask.Instance);

        public Task<IReportingTask> CreateTaskAsync(string statusText, CancellationToken cancellationToken = default)
            => Task.FromResult<IReportingTask>(NoopReportingTask.Instance);

        public Task CompleteAsync(MarkdownString completionText, CompletionState completionState = CompletionState.Completed, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CompleteAsync(string completionText, CompletionState completionState = CompletionState.Completed, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Log(LogLevel logLevel, MarkdownString message)
        {
        }

        public void Log(LogLevel logLevel, string message)
        {
        }

        [Obsolete("Use Log(LogLevel, string) or Log(LogLevel, MarkdownString) instead.")]
        public void Log(LogLevel logLevel, string message, bool enableMarkdown)
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoopReportingTask : IReportingTask
    {
        public static NoopReportingTask Instance { get; } = new();

        public Task UpdateAsync(MarkdownString statusText, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(string statusText, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CompleteAsync(MarkdownString completionMessage, CompletionState completionState = CompletionState.Completed, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CompleteAsync(string? completionMessage = null, CompletionState completionState = CompletionState.Completed, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

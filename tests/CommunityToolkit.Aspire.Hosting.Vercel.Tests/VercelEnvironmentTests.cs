#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES003
#pragma warning disable ASPIREPIPELINES004
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));
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
                Assert.Equal("vercel-publish-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal([WellKnownPipelineSteps.ValidateComputeEnvironments], step.DependsOnSteps);
                Assert.Equal([WellKnownPipelineSteps.Publish, WellKnownPipelineSteps.Deploy], step.RequiredBySteps);
            },
            step =>
            {
                Assert.Equal("vercel-deploy-prereq-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal([WellKnownPipelineSteps.ValidateComputeEnvironments], step.DependsOnSteps);
                Assert.Equal([WellKnownPipelineSteps.Deploy], step.RequiredBySteps);
            },
            step =>
            {
                Assert.Equal("vercel-deploy-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal(["vercel-deploy-prereq-vercel"], step.DependsOnSteps);
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
    public async Task DestroyPipelineStepDoesNotValidateVercelCliWhenStateIsMissing()
    {
        var runner = new FakeVercelCliRunner(new VercelCliResult(1, "", "not logged in"));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
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
        Assert.Same(api, Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment)).Resource);
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

        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));
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

        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));
        Assert.IsAssignableFrom<ContainerResource>(entry.Resource);
        Assert.Equal(sourceRoot.Path, entry.SourceRoot);
        Assert.NotNull(entry.Dockerfile.DockerfileFactory);
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

        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));
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

        var vercelEntry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, vercelEnvironment));
        var otherEntry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, otherEnvironment));

        Assert.Equal("api", vercelEntry.Resource.Name);
        Assert.Equal("worker", otherEntry.Resource.Name);
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

        Assert.Equal(Path.Combine(outputRoot.Path, VercelDeploymentStep.DeploymentPlanFileName), planPath);
        using var document = JsonDocument.Parse(File.ReadAllText(planPath));
        var root = document.RootElement;
        Assert.Equal("vercel", root.GetProperty("environment").GetString());
        var deployment = Assert.Single(root.GetProperty("deployments").EnumerateArray());
        Assert.Equal("api", deployment.GetProperty("resourceName").GetString());
        Assert.Equal("Dockerfile", deployment.GetProperty("dockerfilePath").GetString());
        Assert.Equal("vercel --cwd <api-source-root> deploy --yes", deployment.GetProperty("deployCommand").GetString());
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
        Assert.Equal(Path.Combine(outputRoot.Path, VercelDeploymentStep.DeploymentPlanFileName), summary.Value);
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

        Assert.Equal("vercel --cwd <api-source-root> deploy --yes --env GREETING=<value>", deployment.GetProperty("deployCommand").GetString());
        Assert.Equal("GREETING", Assert.Single(deployment.GetProperty("environmentVariables").EnumerateArray()).GetString());
        Assert.DoesNotContain("hello", File.ReadAllText(planPath));
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

        Assert.Contains("does not have Aspire Dockerfile build metadata", exception.Message);
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
    public async Task WriteDeploymentPlanThrowsForWaitDependencies()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        builder.AddVercelEnvironment("vercel");
        var backend = builder.AddContainer("backend", "backend")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WaitFor(backend);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("wait/dependency ordering", exception.Message);
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

    [Fact]
    public async Task WriteDeploymentPlanAllowsInternalHttpEndpoints()
    {
        await AssertWriteDeploymentPlanSucceedsAsync(static api =>
            api.WithEndpoint(targetPort: 8080, scheme: "http", name: "http", isExternal: false));
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
    public async Task DeployAsyncStagesManagedProjectUsingSlugifiedProjectName()
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
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        string expectedStagingRoot = Path.Combine(tempRoot.Path, "api", "invalid-project");
        var invocation = runner.Invocations[0];
        Assert.Equal(expectedStagingRoot, invocation.WorkingDirectory);

        var deployment = Assert.Single(ReadSavedState(Assert.Single(stateManager.SavedSections)).Deployments);
        Assert.Equal("invalid-project", deployment.ProjectName);
    }

    [Fact]
    public async Task DeployAsyncStagesManagedProjectUsingConfiguredProjectName()
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

        string expectedStagingRoot = Path.Combine(tempRoot.Path, "api", "configured-project");
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, "Dockerfile")));

        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
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

        string[] arguments = VercelDeploymentStep.BuildDeployArguments(options, entry);

        Assert.Equal(["--scope", "team", "--cwd", "/repo/src/api", "deploy", "--yes", "--prod"], arguments);
    }

    [Fact]
    public void BuildDeployArgumentsIncludesTarget()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Target = "preview"
        };
        var entry = CreateDeploymentEntry("/repo/src/api");

        string[] arguments = VercelDeploymentStep.BuildDeployArguments(options, entry);

        Assert.Equal(["--cwd", "/repo/src/api", "deploy", "--yes", "--target", "preview"], arguments);
    }

    [Fact]
    public void BuildDestroyProjectArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] arguments = VercelDeploymentStep.BuildDestroyProjectArguments(options, "api");

        Assert.Equal(["--scope", "team", "project", "remove", "api"], arguments);
    }

    [Fact]
    public void BuildValidateScopeArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] arguments = VercelDeploymentStep.BuildValidateScopeArguments(options);

        Assert.Equal(["--scope", "team", "project", "ls", "--format=json"], arguments);
    }

    [Fact]
    public void BuildInspectDeploymentArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Scope = "team"
        };

        string[] arguments = VercelDeploymentStep.BuildInspectDeploymentArguments(options, "https://api.vercel.app");

        Assert.Equal(["--scope", "team", "inspect", "https://api.vercel.app", "--wait", "--timeout", "120s", "--format=json"], arguments);
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        string[] arguments = await VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        string[] arguments = await VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("entrypoint", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForSecretEnvironmentVariables()
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("API_KEY", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForCompositeSecretEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var secret = builder.AddParameter("api-key", "secret-value", secret: true);
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("AUTH_HEADER", ReferenceExpression.Create($"Bearer {secret.Resource}"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("AUTH_HEADER", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForConnectionStringEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var connectionString = builder.AddConnectionString("db");
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("DATABASE_URL", connectionString);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("DATABASE_URL", exception.Message);
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entries = VercelDeploymentStep.GetDeploymentEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        string[] arguments = await VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entries = VercelDeploymentStep.GetDeploymentEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        string[] arguments = await VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entries = VercelDeploymentStep.GetDeploymentEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        string[] arguments = await VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entries = VercelDeploymentStep.GetDeploymentEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entries = VercelDeploymentStep.GetDeploymentEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entries = VercelDeploymentStep.GetDeploymentEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entries = VercelDeploymentStep.GetDeploymentEntries(model, environment).ToArray();
        var entry = Assert.Single(entries, entry => entry.Resource.Name == "api");

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("command-line arguments", exception.Message);
    }

    [Fact]
    public async Task BuildDeployArgumentsThrowsForDockerBuildArguments()
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
        var entry = Assert.Single(VercelDeploymentStep.GetDeploymentEntries(model, environment));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.BuildDeployArgumentsAsync(
                builder.ExecutionContext,
                NullLogger.Instance,
                environment.GetVercelOptions(),
                entry,
                TestContext.Current.CancellationToken));

        Assert.Contains("build arguments", exception.Message);
    }

    [Fact]
    public async Task DeployAsyncDoesNotRequireContainerRegistryOrImageManager()
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
        Assert.Equal(2, runner.Invocations.Count);
        Assert.Single(stateManager.SavedSections);
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

        var invocation = runner.Invocations[0];
        string expectedStagingRoot = Path.Combine(tempRoot.Path, "api", "vercel-state-project");
        Assert.Equal("vercel", invocation.FileName);
        Assert.Equal(expectedStagingRoot, invocation.WorkingDirectory);
        Assert.Equal(["--scope", "team", "--cwd", expectedStagingRoot, "deploy", "--yes", "--prod", "--env", "GREETING=hello"], invocation.Arguments);
        Assert.Null(invocation.StandardInput);
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, "Dockerfile")));
        Assert.Equal(["--scope", "team", "inspect", "https://api.vercel.app", "--wait", "--timeout", "120s", "--format=json"], runner.Invocations[1].Arguments);

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

        var savedSection = Assert.Single(stateManager.SavedSections);
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
        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
        var deployment = Assert.Single(state.Deployments);
        Assert.Equal("api", deployment.ResourceName);
        Assert.Equal("partial-api", deployment.ProjectName);
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
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
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
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://linked-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var state = ReadSavedState(Assert.Single(stateManager.SavedSections));
        var deployment = Assert.Single(state.Deployments);
        Assert.Equal("linked-project", deployment.ProjectName);
        Assert.Equal("prj_linked", deployment.ProjectId);
        Assert.False(deployment.ManagedByAspire);

        string expectedStagingRoot = Path.Combine(tempRoot.Path, "api", "linked-project-source");
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, ".vercel", "project.json")));
        Assert.False(File.Exists(Path.Combine(expectedStagingRoot, ".vercel", "cache.json")));
    }

    [Fact]
    public async Task DeployAsyncSkipsIgnoredDirectoriesWhenStagingManagedProjects()
    {
        using var sourceRoot = TemporaryDirectory.Create("ignored-staging-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".vercelignore"), "node_modules");
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
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        string expectedStagingRoot = Path.Combine(tempRoot.Path, "api", "ignored-staging-project");
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, "Dockerfile")));
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, ".vercelignore")));
        Assert.False(Directory.Exists(Path.Combine(expectedStagingRoot, ".git")));
        Assert.False(Directory.Exists(Path.Combine(expectedStagingRoot, "node_modules")));
        Assert.False(Directory.Exists(Path.Combine(expectedStagingRoot, ".vercel")));
    }

    [Fact]
    public async Task DeployAsyncWarnsWhenSourceUploadMayIncludeSensitiveOrHeavyFiles()
    {
        using var sourceRoot = TemporaryDirectory.Create("source-warning-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".env"), "SECRET=value");
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, "bin"));
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, "obj"));
        var runner = new FakeVercelCliRunner(
            new VercelCliResult(0, "https://source-warning-project.vercel.app", ""),
            ReadyInspectResult());
        var stateManager = new FakeDeploymentStateManager();
        var logger = new RecordingLogger();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app, logger);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("source root contains files or directories that may be uploaded to Vercel", warning.Message);
        Assert.Contains(".env", warning.Message);
        Assert.Contains("bin/", warning.Message);
        Assert.Contains("obj/", warning.Message);
        Assert.DoesNotContain("SECRET=value", warning.Message);
    }

    [Fact]
    public void GetSourceUploadWarningPathsHonorsVercelIgnore()
    {
        using var sourceRoot = TemporaryDirectory.Create("ignored-source-warning-project");
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".vercelignore"), """
            .env*
            bin/
            obj/
            TestResults/
            coverage/
            """);
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".env.local"), "SECRET=value");
        File.WriteAllText(Path.Combine(sourceRoot.Path, ".env.example"), "DOCUMENTED=value");
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, "bin"));
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, "obj"));
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, "TestResults"));
        Directory.CreateDirectory(Path.Combine(sourceRoot.Path, "coverage"));

        var warnings = VercelDeploymentStep.GetSourceUploadWarningPaths(sourceRoot.Path);

        Assert.Empty(warnings);
    }

    [Fact]
    public async Task DeployAsyncStagesGeneratedDockerfileBeforeRunningVercelCli()
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
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfileFactory(sourceRoot.Path, _ => Task.FromResult("""
                FROM node:22-alpine
                WORKDIR /app
                COPY server.mjs .
                CMD ["node", "server.mjs"]
                """));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var invocation = runner.Invocations[0];
        string expectedStagingRoot = Path.Combine(tempRoot.Path, "api", "generated-vercel-project");
        Assert.Equal(expectedStagingRoot, invocation.WorkingDirectory);
        Assert.Equal(["--cwd", expectedStagingRoot, "deploy", "--yes"], invocation.Arguments);
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, "Dockerfile")));
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, "server.mjs")));
        Assert.Contains("FROM node:22-alpine", File.ReadAllText(Path.Combine(expectedStagingRoot, "Dockerfile")));

        var savedSection = Assert.Single(stateManager.SavedSections);
        Assert.Contains("generated-vercel-project", savedSection.Data.ToJsonString());
    }

    [Fact]
    public async Task DeployAsyncStagesCustomDockerfileNameBeforeRunningVercelCli()
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
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.Services.AddSingleton<IPipelineOutputService>(new FakePipelineOutputService(outputRoot.Path, tempRoot.Path));
        builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.custom");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var invocation = runner.Invocations[0];
        string expectedStagingRoot = Path.Combine(tempRoot.Path, "api", "custom-vercel-project");
        Assert.Equal(expectedStagingRoot, invocation.WorkingDirectory);
        Assert.Equal(["--cwd", expectedStagingRoot, "deploy", "--yes"], invocation.Arguments);
        Assert.Equal("FROM nginx:alpine", File.ReadAllText(Path.Combine(expectedStagingRoot, "Dockerfile")));
        Assert.True(File.Exists(Path.Combine(expectedStagingRoot, "index.html")));
    }

    [Fact]
    public async Task DeployAsyncThrowsWhenVercelCliFails()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(new VercelCliResult(1, "ignored stdout", "deploy failed"));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
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

        Assert.Contains("Failed to deploy resource 'api' to Vercel using 'vercel' (exit code 1). deploy failed", exception.Message);
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
        Assert.Single(runner.Invocations);
        Assert.Empty(stateManager.SavedSections);
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
        Assert.Empty(stateManager.SavedSections);
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
        Assert.Empty(stateManager.SavedSections);
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
        Assert.Empty(stateManager.SavedSections);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesRunsVersionAndWhoami()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "Vercel CLI 54.18.6", ""),
            new(0, "davidfowl-6717", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
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
            invocation => Assert.Equal(["--scope", "team", "project", "ls", "--format=json"], invocation.Arguments));
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
        var vercel = builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidatePrerequisitesAsync(context, environment));

        Assert.Contains("No Dockerfile-backed compute resources target Vercel", exception.Message);
    }

    [Fact]
    public async Task DestroyAsyncDeletesProjectsFromSavedDeploymentState()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(0, "project-list", ""),
            new(0, "", ""),
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
            invocation => Assert.Equal(["--scope", "team", "project", "ls", "--format=json"], invocation.Arguments),
            invocation =>
            {
                Assert.Equal(["--scope", "team", "project", "remove", "a-project"], invocation.Arguments);
                Assert.Equal("y\n", invocation.StandardInput);
            },
            invocation =>
            {
                Assert.Equal(["--scope", "team", "project", "remove", "z-project"], invocation.Arguments);
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
    public async Task DestroyAsyncTreatsMissingManagedProjectAsConverged()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""),
            new(1, "", "Project managed-project not found"));
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
            invocation => Assert.Equal(["project", "remove", "managed-project"], invocation.Arguments));
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
            new(0, "", ""),
            new(1, "", "remove failed"));
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
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        builder.AddVercelEnvironment("vercel");

        using var app = builder.Build();
        var environment = Assert.Single(app.Services.GetRequiredService<DistributedApplicationModel>().Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DestroyAsync(context, environment));

        Assert.Contains("destroy Vercel project 'b-project'", exception.Message);
        Assert.Equal(4, runner.Invocations.Count);
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
        string url = VercelDeploymentStep.GetDeploymentUrl("""
            Vercel CLI 54.18.6
            https://example.vercel.app
            """);

        Assert.Equal("https://example.vercel.app", url);
    }

    [Fact]
    public void GetDeploymentUrlReturnsJsonDeploymentUrl()
    {
        string url = VercelDeploymentStep.GetDeploymentUrl("""
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
        var result = VercelDeploymentStep.GetDeploymentResult("""
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
        var result = VercelDeploymentStep.GetDeploymentResult("""
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
            VercelDeploymentStep.GetDeploymentResult("""
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
        var result = VercelDeploymentStep.GetDeploymentResult("""
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
    public void GetVercelProjectNameReadsVercelProjectLink()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        string vercelDirectory = Path.Combine(sourceRoot.Path, ".vercel");
        Directory.CreateDirectory(vercelDirectory);
        File.WriteAllText(Path.Combine(vercelDirectory, "project.json"), """{"projectName":"linked-project"}""");
        var entry = CreateDeploymentEntry(sourceRoot.Path);
        entry.Resource.Annotations.Add(new VercelProjectOptionsAnnotation("configured-project"));

        string projectName = VercelDeploymentStep.GetVercelProjectName(entry);

        Assert.Equal("linked-project", projectName);
    }

    [Fact]
    public void GetVercelProjectNameFallsBackToSourceRootName()
    {
        using var sourceRoot = TemporaryDirectory.Create("fallback-project");
        var entry = CreateDeploymentEntry(sourceRoot.Path);

        string projectName = VercelDeploymentStep.GetVercelProjectName(entry);

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

    private static VercelDeploymentEntry CreateDeploymentEntry(string sourceRoot)
    {
        string dockerfilePath = Path.Combine(sourceRoot, "Dockerfile");
        return new(
            new ContainerResource("api"),
            sourceRoot,
            dockerfilePath,
            new DockerfileBuildAnnotation(sourceRoot, dockerfilePath, stage: null));
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

        public List<VercelCliInvocation> Invocations { get; } = [];

        public Task<VercelCliResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string? workingDirectory,
            CancellationToken cancellationToken,
            string? standardInput = null)
        {
            Invocations.Add(new(fileName, [.. arguments], workingDirectory, standardInput));

            var result = _results.Count > 0
                ? _results.Dequeue()
                : new VercelCliResult(0, "", "");

            return Task.FromResult(result);
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

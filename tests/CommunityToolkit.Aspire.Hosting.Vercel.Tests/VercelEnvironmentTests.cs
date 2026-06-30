#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
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
            .WithVercelCliPath("vercel-test")
            .WithVercelTarget("preview")
            .WithVercelProductionDeployments()
            .WithVercelTarget("staging");

        var options = vercel.Resource.GetVercelOptions();
        Assert.Equal("vercel-test", options.CliPath);
        Assert.Equal("staging", options.Target);
        Assert.False(options.Production);
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
                Assert.Equal("vercel-destroy-prereq-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal([WellKnownPipelineSteps.Destroy], step.RequiredBySteps);
            },
            step =>
            {
                Assert.Equal("vercel-destroy-vercel", step.Name);
                Assert.Same(vercel.Resource, step.Resource);
                Assert.Equal(["vercel-destroy-prereq-vercel"], step.DependsOnSteps);
                Assert.Equal([WellKnownPipelineSteps.Destroy], step.RequiredBySteps);
            });
    }

    [Fact]
    public void WithVercelCliPathShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<VercelEnvironmentResource> builder = null!;

        var action = () => builder.WithVercelCliPath("vercel");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithVercelCliPathShouldThrowWhenCliPathIsEmpty()
    {
        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        var vercel = builder.AddVercelEnvironment("vercel");

        var action = () => vercel.WithVercelCliPath("");

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("cliPath", exception.ParamName);
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
    public async Task DeployAsyncRunsVercelCliAndSavesDeploymentState()
    {
        using var sourceRoot = TemporaryDirectory.Create("vercel-state-project");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, """
            {
              "deployment": {
                "id": "dpl_123",
                "url": "https://api.vercel.app"
              }
            }
            """, ""));
        var stateManager = new FakeDeploymentStateManager();

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelCliPath("vercel-test")
            .WithVercelScope("team");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile")
            .WithEnvironment("GREETING", "hello");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DeployAsync(context, environment);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("vercel-test", invocation.FileName);
        Assert.Equal(sourceRoot.Path, invocation.WorkingDirectory);
        Assert.Equal(["--scope", "team", "--cwd", sourceRoot.Path, "deploy", "--yes", "--env", "GREETING=hello"], invocation.Arguments);
        Assert.Null(invocation.StandardInput);

        var summary = Assert.Single(context.Summary.Items);
        Assert.Equal("api Vercel deployment", summary.Key);
        Assert.Equal("https://api.vercel.app", summary.Value);

        var savedSection = Assert.Single(stateManager.SavedSections);
        Assert.Equal("communitytoolkit.vercel.vercel", savedSection.SectionName);
        string stateJson = savedSection.Data.ToJsonString();
        Assert.Contains("vercel-state-project", stateJson);
        Assert.Contains("dpl_123", stateJson);
    }

    [Fact]
    public async Task DeployAsyncStagesGeneratedDockerfileBeforeRunningVercelCli()
    {
        using var sourceRoot = TemporaryDirectory.Create("generated-vercel-project");
        using var outputRoot = TemporaryDirectory.Create();
        using var tempRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "server.mjs"), "console.log('hello');");
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, "https://generated.vercel.app", ""));
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

        var invocation = Assert.Single(runner.Invocations);
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
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, "https://custom.vercel.app", ""));
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

        var invocation = Assert.Single(runner.Invocations);
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
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelCliPath("vercel-test");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.DeployAsync(context, environment));

        Assert.Contains("Failed to deploy resource 'api' to Vercel using 'vercel-test' (exit code 1). deploy failed", exception.Message);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesRunsVersionAndWhoami()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(0, "davidfowl-6717", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelCliPath("vercel-test");

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
    public async Task ValidateCliPrerequisitesThrowsWhenWhoamiFails()
    {
        var runner = new FakeVercelCliRunner(
            new(0, "54.18.6", ""),
            new(1, "", "not logged in"));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelCliPath("vercel-test");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment));

        Assert.Contains("Failed to validate Vercel authentication using 'vercel-test' (exit code 1). not logged in", exception.Message);
    }

    [Fact]
    public async Task ValidateCliPrerequisitesThrowsWhenVersionFailsWithStandardOutput()
    {
        var runner = new FakeVercelCliRunner(new VercelCliResult(1, "missing vercel", ""));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelCliPath("vercel-test");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, environment));

        Assert.Contains("Failed to validate Vercel CLI installation using 'vercel-test' (exit code 1). missing vercel", exception.Message);
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
            new(0, "", ""),
            new(0, "", ""));
        var stateManager = new FakeDeploymentStateManager();
        stateManager.SetSection("communitytoolkit.vercel.vercel", JsonSerializer.Serialize(new VercelDeploymentState(
            "vercel",
            [
                new("api", "z-project", "dpl_1", "https://z-project.vercel.app"),
                new("worker", "a-project", null, null),
                new("api2", "z-project", null, null)
            ])));

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest"]);
        builder.Services.AddSingleton<IVercelCliRunner>(runner);
        builder.Services.AddSingleton<IDeploymentStateManager>(stateManager);
        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelCliPath("vercel-test")
            .WithVercelScope("team");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var context = CreatePipelineStepContext(builder, app);

        await VercelDeploymentStep.DestroyAsync(context, environment);

        Assert.Collection(
            runner.Invocations,
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
    public async Task DestroyAsyncFallsBackToConfiguredDeploymentsWhenStateIsMissing()
    {
        using var sourceRoot = TemporaryDirectory.Create("fallback-project");
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile"), "FROM nginx:alpine");
        var runner = new FakeVercelCliRunner(new VercelCliResult(0, "", ""));

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

        await VercelDeploymentStep.DestroyAsync(context, environment);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal(["project", "remove", "fallback-project"], invocation.Arguments);
    }

    [Fact]
    public async Task DestroyAsyncAddsSummaryWhenNoDeploymentsExist()
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
        Assert.Contains("No Vercel deployments", summary.Value);
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
    public void GetDeploymentResultFallsBackWhenJsonHasNoUrl()
    {
        var result = VercelDeploymentStep.GetDeploymentResult("""
            {
              "deployment": {
                "id": "dpl_no_url"
              }
            }
            """);

        Assert.Null(result.DeploymentId);
        Assert.Equal("""
            {
              "deployment": {
                "id": "dpl_no_url"
              }
            }
            """.Trim(), result.DeploymentUrl);
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

        Assert.Equal("vercel", options.CliPath);
        Assert.Null(options.Scope);
        Assert.Null(options.Target);
        Assert.False(options.Production);
    }

    private static PipelineStepContext CreatePipelineStepContext(
        IDistributedApplicationBuilder builder,
        DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var pipelineContext = new PipelineContext(
            model,
            builder.ExecutionContext,
            app.Services,
            NullLogger.Instance,
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

    private sealed record VercelCliInvocation(
        string FileName,
        string[] Arguments,
        string? WorkingDirectory,
        string? StandardInput);

    private sealed class FakeDeploymentStateManager : IDeploymentStateManager
    {
        private readonly Dictionary<string, DeploymentStateSection> _sections = new(StringComparer.Ordinal);

        public string? StateFilePath => null;

        public List<DeploymentStateSection> SavedSections { get; } = [];

        public List<DeploymentStateSection> DeletedSections { get; } = [];

        public void SetSection(string sectionName, string value)
            => _sections[sectionName] = new(sectionName, new JsonObject { ["value"] = value }, 0);

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

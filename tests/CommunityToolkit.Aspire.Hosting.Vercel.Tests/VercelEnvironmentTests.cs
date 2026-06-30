#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Vercel;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

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
    public void RunModeDoesNotAddVercelEnvironmentOrDeploymentAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api").PublishAsVercel(vercel);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = Assert.Single(model.Resources.OfType<ContainerResource>());

        Assert.DoesNotContain(model.Resources, resource => resource is VercelEnvironmentResource);
        Assert.False(api.TryGetLastAnnotation<VercelDeploymentAnnotation>(out _));
    }

    [Fact]
    public void PublishModeAddsVercelEnvironmentAndDeploymentAnnotation()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);

        var vercel = builder.AddVercelEnvironment("vercel")
            .WithVercelScope("team")
            .WithVercelTarget("preview");

        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .PublishAsVercel(vercel);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());
        var api = Assert.Single(model.Resources.OfType<ContainerResource>());

        Assert.Same(environment, api.GetComputeEnvironment());
        Assert.True(api.TryGetLastAnnotation<VercelDeploymentAnnotation>(out var deployment));
        Assert.Equal("Dockerfile.vercel", deployment.DockerfilePath);

        var options = environment.GetVercelOptions();
        Assert.Equal("team", options.Scope);
        Assert.Equal("preview", options.Target);
        Assert.False(options.Production);
    }

    [Fact]
    public async Task WriteDeploymentPlanWritesExpectedJson()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .PublishAsVercel(vercel);

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
        Assert.Equal("Dockerfile.vercel", deployment.GetProperty("dockerfilePath").GetString());
        Assert.Equal("vercel --cwd <api-source-root> deploy --yes", deployment.GetProperty("deployCommand").GetString());
    }

    [Fact]
    public async Task WriteDeploymentPlanProcessesEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        using var outputRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", outputRoot.Path]);
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .WithEnvironment("GREETING", "hello")
            .PublishAsVercel(vercel);

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
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .PublishAsVercel(vercel);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = Assert.Single(model.Resources.OfType<VercelEnvironmentResource>());

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            VercelDeploymentStep.WriteDeploymentPlanAsync(model, environment, outputRoot.Path, TestContext.Current.CancellationToken));

        Assert.Contains("Dockerfile.vercel", exception.Message);
    }

    [Fact]
    public void BuildDeployArgumentsIncludesConfiguredOptions()
    {
        var options = new VercelEnvironmentOptionsAnnotation
        {
            Production = true,
            Scope = "team"
        };
        var entry = new VercelDeploymentEntry(new ContainerResource("api"), "/repo/src/api", "Dockerfile.vercel");

        string[] arguments = VercelDeploymentStep.BuildDeployArguments(options, entry);

        Assert.Equal(["--scope", "team", "--cwd", "/repo/src/api", "deploy", "--yes", "--prod"], arguments);
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
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .WithEnvironment("GREETING", "hello")
            .PublishAsVercel(vercel);

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
    public async Task BuildDeployArgumentsThrowsForSecretEnvironmentVariables()
    {
        using var sourceRoot = TemporaryDirectory.Create();
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var secret = builder.AddParameter("api-key", "secret-value", secret: true);
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .WithEnvironment("API_KEY", secret)
            .PublishAsVercel(vercel);

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
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var connectionString = builder.AddConnectionString("db");
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .WithEnvironment("DATABASE_URL", connectionString)
            .PublishAsVercel(vercel);

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
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .WithArgs("--verbose")
            .PublishAsVercel(vercel);

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
        File.WriteAllText(Path.Combine(sourceRoot.Path, "Dockerfile.vercel"), "FROM nginx:alpine");

        var builder = DistributedApplication.CreateBuilder(["--publisher", "manifest", "--output-path", Path.Combine(sourceRoot.Path, "out")]);
        var vercel = builder.AddVercelEnvironment("vercel");
        builder.AddContainer("api", "api")
            .WithDockerfile(sourceRoot.Path, "Dockerfile.vercel")
            .WithBuildArg("FOO", "bar")
            .PublishAsVercel(vercel);

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
        var entry = new VercelDeploymentEntry(new ContainerResource("api"), sourceRoot.Path, "Dockerfile.vercel");

        string projectName = VercelDeploymentStep.GetVercelProjectName(entry);

        Assert.Equal("linked-project", projectName);
    }

    [Fact]
    public void GetVercelProjectNameFallsBackToSourceRootName()
    {
        using var sourceRoot = TemporaryDirectory.Create("fallback-project");
        var entry = new VercelDeploymentEntry(new ContainerResource("api"), sourceRoot.Path, "Dockerfile.vercel");

        string projectName = VercelDeploymentStep.GetVercelProjectName(entry);

        Assert.Equal("fallback-project", projectName);
    }

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
}

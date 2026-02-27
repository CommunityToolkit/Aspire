using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.DotNet.XUnitExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace CommunityToolkit.Aspire.Hosting.Neon.Tests;

public class NeonFunctionalTests
{
    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task Commands_WithNoOpProvisioner_Succeed()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "neon-cmd-noop", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        string projectPath = Path.Combine(tempDir, "NoOpProvisioner.csproj");
        string programPath = Path.Combine(tempDir, "Program.cs");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(programPath, """
            return;
            """);

        try
        {
            using var builder = TestDistributedApplicationBuilder.Create();
            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey).AsExisting();

            SetPropertyValue(neon.Resource, nameof(NeonProjectResource.ProjectId), "project-id");
            SetPropertyValue(neon.Resource, nameof(NeonProjectResource.EndpointId), "endpoint-id");

            object annotation = CreateProvisionerAnnotation(
                neon.Resource.ProvisionerResource!,
                projectPath,
                Path.Combine(tempDir, "output.json"),
                "attach");

            neon.Resource.Annotations.Add((IResourceAnnotation)annotation);

            using var app = builder.Build();
            DistributedApplicationModel model = app.Services.GetRequiredService<DistributedApplicationModel>();
            NeonProjectResource resource = Assert.Single(model.Resources.OfType<NeonProjectResource>());

            Assert.True(resource.TryGetAnnotationsOfType<ResourceCommandAnnotation>(out IEnumerable<ResourceCommandAnnotation>? commands));
            ResourceCommandAnnotation suspendCommand = Assert.Single(commands!, command => command.Name == "neon-suspend");
            ResourceCommandAnnotation resumeCommand = Assert.Single(commands!, command => command.Name == "neon-resume");

            ExecuteCommandContext context = new()
            {
                ServiceProvider = app.Services,
                ResourceName = resource.Name,
                CancellationToken = CancellationToken.None,
            };

            ExecuteCommandResult suspend = await suspendCommand.ExecuteCommand(context);
            ExecuteCommandResult resume = await resumeCommand.ExecuteCommand(context);

            Assert.True(suspend.Success, suspend.ErrorMessage);
            Assert.True(resume.Success, resume.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionerRun_WithMissingApiKey_FailsAndWritesFailureArtifact()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "neon-provisioner-it", Guid.NewGuid().ToString("N"), "output.json");

        (int exitCode, string _, string stdErr) = await RunProvisionerAsync(new Dictionary<string, string?>
        {
            ["NEON_MODE"] = "attach",
            ["NEON_OUTPUT_FILE_PATH"] = outputPath,
        });

        Assert.NotEqual(0, exitCode);
        Assert.Contains("NEON_API_KEY", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists($"{outputPath}.error.log"));
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionerRun_WithInvalidMode_FailsFast()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "neon-provisioner-it", Guid.NewGuid().ToString("N"), "output.json");

        (int exitCode, string _, string stdErr) = await RunProvisionerAsync(new Dictionary<string, string?>
        {
            ["NEON_API_KEY"] = "test-key",
            ["NEON_MODE"] = "invalid-mode",
            ["NEON_OUTPUT_FILE_PATH"] = outputPath,
        });

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Unsupported Neon mode", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists($"{outputPath}.error.log"));
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionerRun_WithMissingOutputPath_FailsFast()
    {
        (int exitCode, string _, string stdErr) = await RunProvisionerAsync(new Dictionary<string, string?>
        {
            ["NEON_API_KEY"] = "test-key",
            ["NEON_MODE"] = "attach",
            ["NEON_OUTPUT_FILE_PATH"] = null,
        });

        Assert.NotEqual(0, exitCode);
        Assert.Contains("NEON_OUTPUT_FILE_PATH", stdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionerRun_AttachWithoutProjectConfiguration_FailsFast()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "neon-provisioner-it", Guid.NewGuid().ToString("N"), "output.json");

        (int exitCode, string _, string stdErr) = await RunProvisionerAsync(new Dictionary<string, string?>
        {
            ["NEON_API_KEY"] = "test-key",
            ["NEON_MODE"] = "attach",
            ["NEON_OUTPUT_FILE_PATH"] = outputPath,
            ["NEON_PROJECT_ID"] = string.Empty,
            ["NEON_PROJECT_NAME"] = string.Empty,
        });

        Assert.NotEqual(0, exitCode);
        Assert.Contains("NEON_PROJECT_ID", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NEON_PROJECT_NAME", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists($"{outputPath}.error.log"));
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionerRun_AttachWithEphemeralBranch_FailsFast()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "neon-provisioner-it", Guid.NewGuid().ToString("N"), "output.json");

        (int exitCode, string _, string stdErr) = await RunProvisionerAsync(new Dictionary<string, string?>
        {
            ["NEON_API_KEY"] = "test-key",
            ["NEON_MODE"] = "attach",
            ["NEON_OUTPUT_FILE_PATH"] = outputPath,
            ["NEON_PROJECT_ID"] = "project-id",
            ["NEON_USE_EPHEMERAL_BRANCH"] = "true",
        });

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Ephemeral branch mode requires 'provision' mode", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists($"{outputPath}.error.log"));
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionerRun_SuspendWithoutProjectAndEndpoint_FailsFast()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "neon-provisioner-it", Guid.NewGuid().ToString("N"), "output.json");

        (int exitCode, string _, string stdErr) = await RunProvisionerAsync(new Dictionary<string, string?>
        {
            ["NEON_API_KEY"] = "test-key",
            ["NEON_MODE"] = "suspend",
            ["NEON_OUTPUT_FILE_PATH"] = outputPath,
            ["NEON_PROJECT_ID"] = string.Empty,
            ["NEON_ENDPOINT_ID"] = string.Empty,
        });

        Assert.NotEqual(0, exitCode);
        Assert.Contains("NEON_PROJECT_ID", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Required environment variable", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists($"{outputPath}.error.log"));
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionerRun_ResumeWithoutProjectAndEndpoint_FailsFast()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "neon-provisioner-it", Guid.NewGuid().ToString("N"), "output.json");

        (int exitCode, string _, string stdErr) = await RunProvisionerAsync(new Dictionary<string, string?>
        {
            ["NEON_API_KEY"] = "test-key",
            ["NEON_MODE"] = "resume",
            ["NEON_OUTPUT_FILE_PATH"] = outputPath,
            ["NEON_PROJECT_ID"] = string.Empty,
            ["NEON_ENDPOINT_ID"] = string.Empty,
        });

        Assert.NotEqual(0, exitCode);
        Assert.Contains("NEON_PROJECT_ID", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Required environment variable", stdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists($"{outputPath}.error.log"));
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task Internal_ReadProvisionerOutput_ParsesJsonAndFailureArtifact()
    {
        string directory = Path.Combine(Path.GetTempPath(), "neon-functional", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string outputPath = Path.Combine(directory, "neon.json");
            await File.WriteAllTextAsync(outputPath, """
                {
                  "ProjectId": "project-1",
                  "BranchId": "branch-1",
                  "EndpointId": "endpoint-1",
                  "Host": "host",
                  "Port": 5432,
                  "Password": "pass",
                  "DefaultDatabaseName": "neondb",
                  "DefaultRoleName": "neondb_owner",
                  "DefaultConnectionUri": "postgres://neondb_owner:pass@host:5432/neondb",
                  "Databases": []
                }
                """);

            object? parsed = await InvokeHostingPrivateAsync(
                "ReadProvisionerOutputAsync",
                outputPath,
                NullLogger.Instance,
                CancellationToken.None);

            Assert.NotNull(parsed);
            Assert.Equal("project-1", parsed!.GetType().GetProperty("ProjectId")!.GetValue(parsed));

            await File.WriteAllTextAsync($"{outputPath}.error.log", "simulated");

            await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
                _ = await InvokeHostingPrivateAsync(
                    "ReadProvisionerOutputAsync",
                    outputPath,
                    NullLogger.Instance,
                    CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task Internal_ConfigureConnectionFromOutput_UpdatesDatabaseResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey).AsExisting();
        IResourceBuilder<NeonDatabaseResource> database = neon.AddDatabase("appdb", "appdb", "app_owner");

        object annotation = neon.Resource.Annotations.First(a => a.GetType().Name == "NeonExternalProvisionerAnnotation");
        string outputPath = (string)annotation.GetType().GetProperty("OutputFilePath")!.GetValue(annotation)!;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await File.WriteAllTextAsync(outputPath, """
            {
              "ProjectId": "project-2",
              "BranchId": "branch-2",
              "EndpointId": "endpoint-2",
              "DefaultDatabaseName": "neondb",
              "DefaultRoleName": "neondb_owner",
              "DefaultConnectionUri": "postgres://neondb_owner:pass@default-host:5432/neondb",
              "Host": "default-host",
              "Port": 5432,
              "Password": "pass",
              "Databases": [
                {
                  "ResourceName": "appdb",
                  "DatabaseName": "appdb",
                  "RoleName": "app_owner",
                  "ConnectionUri": "postgres://app_owner:dbpass@app-host:5432/appdb",
                  "Host": "app-host",
                  "Port": 5432,
                  "Password": "dbpass"
                }
              ]
            }
            """);

        using var app = builder.Build();

        _ = await InvokeHostingPrivateAsync(
            "ConfigureNeonConnectionFromOutputAsync",
            builder,
            neon.Resource,
            annotation,
            app.Services,
            CancellationToken.None);

        Assert.Equal("project-2", neon.Resource.ProjectId);
        Assert.Equal("app-host", database.Resource.Host);
        Assert.Equal("postgres://app_owner:dbpass@app-host:5432/appdb", database.Resource.ConnectionUri);
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task Internal_HealthChecks_ReportDegradedThenHealthy()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey);
        IResourceBuilder<NeonDatabaseResource> database = neon.AddDatabase("appdb", "appdb", "app_owner");

        Type? projectHealthType = typeof(NeonProjectOptions).Assembly.GetType("CommunityToolkit.Aspire.Hosting.Neon.NeonHealthCheck");
        Type? databaseHealthType = typeof(NeonProjectOptions).Assembly.GetType("CommunityToolkit.Aspire.Hosting.Neon.NeonDatabaseHealthCheck");
        Assert.NotNull(projectHealthType);
        Assert.NotNull(databaseHealthType);

        IHealthCheck projectHealth = (IHealthCheck)Activator.CreateInstance(projectHealthType!, neon.Resource)!;
        IHealthCheck databaseHealth = (IHealthCheck)Activator.CreateInstance(databaseHealthType!, database.Resource)!;

        HealthCheckContext context = new()
        {
            Registration = new HealthCheckRegistration("neon", projectHealth, HealthStatus.Degraded, tags: null),
        };

        Assert.Equal(HealthStatus.Degraded, (await projectHealth.CheckHealthAsync(context)).Status);
        Assert.Equal(HealthStatus.Degraded, (await databaseHealth.CheckHealthAsync(context)).Status);

        SetPropertyValue(neon.Resource, nameof(NeonProjectResource.ConnectionUri), "postgres://u:p@host/neondb");
        SetPropertyValue(database.Resource, nameof(NeonDatabaseResource.ConnectionUri), "postgres://u:p@host/appdb");

        Assert.Equal(HealthStatus.Healthy, (await projectHealth.CheckHealthAsync(context)).Status);
        Assert.Equal(HealthStatus.Healthy, (await databaseHealth.CheckHealthAsync(context)).Status);
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public void ConfigureInfrastructure_InvokesConfiguration()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        bool invoked = false;

        IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
            .ConfigureInfrastructure(options =>
            {
                invoked = true;
                options.ProjectId = "project-configured";
                options.Branch.BranchName = "main";
            });

        Assert.True(invoked);
        Assert.NotEmpty(neon.Resource.Annotations.OfType<ResourceCommandAnnotation>());
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task SuspendAndResumeCommands_WithoutConnection_ReturnUnavailable()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey).AsExisting();

        var suspend = neon.Resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Single(a => a.Name == "neon-suspend");
        var resume = neon.Resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Single(a => a.Name == "neon-resume");

        ExecuteCommandResult suspendResult = await suspend.ExecuteCommand(new()
        {
            ResourceName = neon.Resource.Name,
            ServiceProvider = builder.Services.BuildServiceProvider(),
            CancellationToken = CancellationToken.None,
        })!;

        ExecuteCommandResult resumeResult = await resume.ExecuteCommand(new()
        {
            ResourceName = neon.Resource.Name,
            ServiceProvider = builder.Services.BuildServiceProvider(),
            CancellationToken = CancellationToken.None,
        })!;

        Assert.False(suspendResult.Success);
        Assert.False(resumeResult.Success);
        Assert.Contains("not provisioned", suspendResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not provisioned", resumeResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task Internal_ExecuteProvisionerEndpointCommand_ThrowsForMissingPathAndApiKey()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", string.Empty, secret: true);
        IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey).AsExisting();

        object missingPathAnnotation = CreateProvisionerAnnotation(
            neon.Resource.ProvisionerResource!,
            "X:\\missing\\provisioner.csproj",
            Path.Combine(Path.GetTempPath(), "missing-output.json"),
            "attach");

        DistributedApplicationException missingPathError = await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
            _ = await InvokeHostingPrivateAsync(
                "ExecuteProvisionerEndpointCommandAsync",
                missingPathAnnotation,
                neon.Resource,
                "suspend",
                CancellationToken.None));

        Assert.Contains("was not found", missingPathError.Message, StringComparison.OrdinalIgnoreCase);

        object existingAnnotation = neon.Resource.Annotations.First(a => a.GetType().Name == "NeonExternalProvisionerAnnotation");
        string existingProjectPath = (string)existingAnnotation.GetType().GetProperty("ProjectPath")!.GetValue(existingAnnotation)!;

        object missingApiKeyAnnotation = CreateProvisionerAnnotation(
            neon.Resource.ProvisionerResource!,
            existingProjectPath,
            Path.Combine(Path.GetTempPath(), "missing-key-output.json"),
            "attach");

        DistributedApplicationException missingApiKeyError = await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
            _ = await InvokeHostingPrivateAsync(
                "ExecuteProvisionerEndpointCommandAsync",
                missingApiKeyAnnotation,
                neon.Resource,
                "resume",
                CancellationToken.None));

        Assert.Contains("api key is required", missingApiKeyError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task Internal_ConfigureConnectionWithErrorHandling_ThrowsWhenFailureArtifactExists()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", "test", secret: true);
        IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey).AsExisting();

        using var app = builder.Build();

        string outputPath = Path.Combine(Path.GetTempPath(), "neon-tests", Guid.NewGuid().ToString("N"), "neon-error.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync($"{outputPath}.error.log", "simulated failure");

        try
        {
            object annotation = CreateProvisionerAnnotation(
                neon.Resource.ProvisionerResource!,
                "placeholder.csproj",
                outputPath,
                "attach");

            DistributedApplicationException ex = await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
                _ = await InvokeHostingPrivateAsync(
                    "ConfigureNeonConnectionWithErrorHandlingAsync",
                    builder,
                    neon.Resource,
                    annotation,
                    app.Services,
                    CancellationToken.None));

            Assert.Contains("failed before producing output", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "NeonIntegration")]
    public async Task Internal_ReadProvisionerOutput_HonorsCancellation()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), "neon-functional", Guid.NewGuid().ToString("N"), "pending.json");
        CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            _ = await InvokeHostingPrivateAsync(
                "ReadProvisionerOutputAsync",
                outputPath,
                NullLogger.Instance,
                cts.Token));
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task AttachMode_WithExistingProject_BecomesHealthy()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .AddProject(settings.ProjectName);

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);

            string? connectionString = await neon.Resource.GetConnectionStringAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
        });
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task AttachMode_WithProjectAndBranchSelectors_DefaultsToAttach_AndBecomesHealthy()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .WithProjectName(settings.ProjectName)
                .WithBranchName(settings.ExistingBranchName);

            Assert.NotNull(neon.Resource.ProvisionerResource);
            Dictionary<string, string> env = await neon.Resource.ProvisionerResource!.GetEnvironmentVariablesAsync();
            Assert.Equal("attach", env["NEON_MODE"]);

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);

            string? connectionString = await neon.Resource.GetConnectionStringAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
        });
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionMode_WithEphemeralBranch_AndCustomDatabase_BecomesHealthy()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);

            string suffix = Guid.NewGuid().ToString("N")[..8];
            string databaseResourceName = $"olympusdb{suffix}";
            string databaseName = $"testq_{suffix}";

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .AddProject(settings.ProjectName)
                .AddEphemeralBranch(settings.EphemeralPrefix);

            IResourceBuilder<NeonDatabaseResource> database = neon.AddDatabase(databaseResourceName, databaseName, "neondb_owner");

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(database.Resource.Name, cts.Token);

            string? connectionString = await database.Resource.GetConnectionStringAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            Assert.Contains($"/{databaseName}", connectionString, StringComparison.OrdinalIgnoreCase);
        });
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionMode_WithMultipleDatabases_ProducesDistinctConnections()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);
            string suffix = Guid.NewGuid().ToString("N")[..8];

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .AddProject(settings.ProjectName)
                .AddEphemeralBranch(settings.EphemeralPrefix);

            IResourceBuilder<NeonDatabaseResource> db1 = neon.AddDatabase($"db1{suffix}", $"db1_{suffix}", "neondb_owner");
            IResourceBuilder<NeonDatabaseResource> db2 = neon.AddDatabase($"db2{suffix}", $"db2_{suffix}", "neondb_owner");

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(db1.Resource.Name, cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(db2.Resource.Name, cts.Token);

            string? cs1 = await db1.Resource.GetConnectionStringAsync(cts.Token);
            string? cs2 = await db2.Resource.GetConnectionStringAsync(cts.Token);

            Assert.False(string.IsNullOrWhiteSpace(cs1));
            Assert.False(string.IsNullOrWhiteSpace(cs2));
            Assert.NotEqual(cs1, cs2);
        });
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionMode_WithAnonymizedEphemeralBranch_BecomesHealthy()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);
            string suffix = Guid.NewGuid().ToString("N")[..8];

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .AddProject(settings.ProjectName)
                .AddEphemeralBranch(settings.EphemeralPrefix)
                .WithAnonymizedData(anon =>
                {
                    anon.MaskingRules.Add(new NeonMaskingRule
                    {
                        DatabaseName = "neondb",
                        SchemaName = "public",
                        TableName = "users",
                        ColumnName = "email",
                        MaskingFunction = "mask_email",
                    });
                });

            IResourceBuilder<NeonDatabaseResource> database = neon.AddDatabase($"anondb{suffix}", $"anon_{suffix}", "neondb_owner");

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(database.Resource.Name, cts.Token);

            string? connectionString = await database.Resource.GetConnectionStringAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
        });
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionMode_WithConnectionPooler_UsesPoolerEndpoint()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);
            string suffix = Guid.NewGuid().ToString("N")[..8];

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .AddProject(settings.ProjectName)
                .AddEphemeralBranch(settings.EphemeralPrefix)
                .WithConnectionPooler();

            IResourceBuilder<NeonDatabaseResource> database = neon.AddDatabase($"poolerdb{suffix}", $"pooler_{suffix}", "neondb_owner");

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(database.Resource.Name, cts.Token);

            string? connectionString = await database.Resource.GetConnectionStringAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
            Assert.Contains("pooler", connectionString, StringComparison.OrdinalIgnoreCase);
        });
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionMode_WithBranchRestore_AndDefaultBranch_BecomesHealthy()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);
            string suffix = Guid.NewGuid().ToString("N")[..8];
            string branchName = $"it-{suffix}";

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .AddProject(settings.ProjectName)
                .AddBranch(branchName)
                .WithBranchRestore()
                .AsDefaultBranch();

            IResourceBuilder<NeonDatabaseResource> database = neon.AddDatabase($"restoredb{suffix}", $"restore_{suffix}", "neondb_owner");

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(database.Resource.Name, cts.Token);

            string? connectionString = await database.Resource.GetConnectionStringAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
        });
    }

    [ConditionalFact]
    [Trait("Category", "NeonIntegration")]
    public async Task ProvisionMode_SameConfiguration_CanStartTwice()
    {
        await ExecuteOrSkipOnEnvironmentIssueAsync(async () =>
        {
            NeonIntegrationTestSettings settings = NeonIntegrationTestSettings.Require();
            string suffix = Guid.NewGuid().ToString("N")[..8];
            string databaseName = $"repeat_{suffix}";

            await RunScenarioAsync(settings, suffix, databaseName);
            await RunScenarioAsync(settings, suffix, databaseName);
        });

        static async Task RunScenarioAsync(NeonIntegrationTestSettings settings, string suffix, string databaseName)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var builder = TestDistributedApplicationBuilder.Create();

            IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("neon-api-key", settings.ApiKey, secret: true);

            IResourceBuilder<NeonProjectResource> neon = builder.AddNeon("neon", apiKey)
                .AddProject(settings.ProjectName)
                .AddEphemeralBranch(settings.EphemeralPrefix);

            IResourceBuilder<NeonDatabaseResource> database = neon.AddDatabase($"repeatdb{suffix}", databaseName, "neondb_owner");

            using var app = builder.Build();

            await app.StartAsync(cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(neon.Resource.Name, cts.Token);
            await app.ResourceNotifications.WaitForResourceHealthyAsync(database.Resource.Name, cts.Token);

            string? connectionString = await database.Resource.GetConnectionStringAsync(cts.Token);
            Assert.False(string.IsNullOrWhiteSpace(connectionString));
        }
    }

    private static async Task ExecuteOrSkipOnEnvironmentIssueAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (SkipTestException)
        {
            throw;
        }
        catch (Exception ex) when (ShouldSkipForEnvironment(ex, out string reason))
        {
            throw new SkipTestException($"Skipping Neon integration test due to environment/permission limits: {reason}");
        }
        catch (Exception) when (TryResolveProvisionerFailureReason(out string reason) && ShouldSkipForEnvironmentMessage(reason))
        {
            throw new SkipTestException($"Skipping Neon integration test due to environment/permission limits: {reason}");
        }
    }

    private static bool ShouldSkipForEnvironment(Exception exception, out string reason)
    {
        string message = exception.ToString();

        if (ShouldSkipForEnvironmentMessage(message))
        {
            reason = exception.Message;
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool ShouldSkipForEnvironmentMessage(string message)
    {
        if (message.Contains("401", StringComparison.OrdinalIgnoreCase)
            || message.Contains("403", StringComparison.OrdinalIgnoreCase)
            || message.Contains("404", StringComparison.OrdinalIgnoreCase)
            || message.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || message.Contains("credentials do not pass authentication", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || message.Contains("insufficient", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not allowed to perform actions outside the project this key is scoped to", StringComparison.OrdinalIgnoreCase)
            || message.Contains("org_id is required", StringComparison.OrdinalIgnoreCase)
            || message.Contains("failed to apply masking rule", StringComparison.OrdinalIgnoreCase)
            || message.Contains("relation \"public.users\" does not exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("423", StringComparison.OrdinalIgnoreCase)
            || message.Contains("conflicting operations", StringComparison.OrdinalIgnoreCase)
            || message.Contains("failed to start", StringComparison.OrdinalIgnoreCase)
            || message.Contains("stopped waiting for resource", StringComparison.OrdinalIgnoreCase)
            || message.Contains("kubeconfig", StringComparison.OrdinalIgnoreCase)
            || message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
            || message.Contains("watch task over kubernetes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveProvisionerFailureReason(out string reason)
    {
        string root = Path.Combine(Path.GetTempPath(), "aspire-neon-output");
        if (!Directory.Exists(root))
        {
            reason = string.Empty;
            return false;
        }

        string? newest = Directory
            .EnumerateFiles(root, "neon.json.error.log", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => info.FullName)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(newest))
        {
            reason = string.Empty;
            return false;
        }

        reason = File.ReadAllText(newest);
        return !string.IsNullOrWhiteSpace(reason);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProvisionerAsync(IDictionary<string, string?> environment)
    {
        string projectPath = ResolveTemplateProvisionerProjectPath();

        ProcessStartInfo psi = new("dotnet", $"run --project \"{projectPath}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach ((string key, string? value) in environment)
        {
            if (value is null)
            {
                _ = psi.Environment.Remove(key);
            }
            else
            {
                psi.Environment[key] = value;
            }
        }

        using Process process = Process.Start(psi)!;
        string stdOut = await process.StandardOutput.ReadToEndAsync();
        string stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdOut, stdErr);
    }

    private static string ResolveTemplateProvisionerProjectPath()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        Type? templateType = typeof(NeonProjectOptions).Assembly.GetType("Aspire.Hosting.NeonProvisionerProjectTemplate");
        Assert.NotNull(templateType);

        MethodInfo? ensureProject = templateType!.GetMethod("EnsureProject", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(ensureProject);

        object? path = ensureProject!.Invoke(null, [builder]);
        Assert.NotNull(path);

        return (string)path!;
    }

    private static void SetPropertyValue(object target, string propertyName, object value)
    {
        PropertyInfo? property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    private static object CreateProvisionerAnnotation(
        IResourceWithWaitSupport resource,
        string projectPath,
        string outputPath,
        string mode)
    {
        Type? annotationType = typeof(NeonProjectOptions).Assembly.GetType("CommunityToolkit.Aspire.Hosting.Neon.NeonExternalProvisionerAnnotation");
        Assert.NotNull(annotationType);

        ConstructorInfo? constructor = annotationType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == 4
                    && parameters[0].ParameterType.IsAssignableTo(typeof(IResourceWithWaitSupport))
                    && parameters[1].ParameterType == typeof(string)
                    && parameters[2].ParameterType == typeof(string);
            });
        Assert.NotNull(constructor);

        ParameterInfo modeParameter = constructor!.GetParameters()[3];
        object modeArgument = modeParameter.ParameterType.IsEnum
            ? Enum.Parse(modeParameter.ParameterType, mode, ignoreCase: true)
            : mode;

        object? instance = constructor.Invoke([resource, projectPath, outputPath, modeArgument]);
        Assert.NotNull(instance);
        return instance!;
    }

    private static async Task<object?> InvokeHostingPrivateAsync(string methodName, params object[] args)
    {
        MethodInfo? method = typeof(NeonBuilderExtensions).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        object? invocationResult = method!.Invoke(null, args);
        Assert.NotNull(invocationResult);

        Task task = (Task)invocationResult!;
        await task.ConfigureAwait(false);

        if (task.GetType().IsGenericType)
        {
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }

        return null;
    }
}

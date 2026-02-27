using System.Text.Json;
using System.Collections.Immutable;
using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Neon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Neon resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class NeonBuilderExtensions
{
    /// <summary>
    /// Adds a Neon Postgres resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="apiKey">The parameter builder providing the Neon API key.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a Neon project and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var apiKey = builder.AddParameter("neon-api-key", "your-key", secret: true);
    ///
    /// var neon = builder.AddNeon("neon", apiKey)
    ///     .AddProject("aspire-neon")
    ///     .AddBranch("dev");
    ///
    /// var db = neon.AddDatabase("appdb", "appdb");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///     .WithReference(db);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<NeonProjectResource> AddNeon(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> apiKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(apiKey);

        var resource = new NeonProjectResource(name, apiKey.Resource);

        var healthCheckKey = $"{name}_neon_health";

        CustomResourceSnapshot initialState = new()
        {
            State = new(KnownResourceStates.Starting, KnownResourceStateStyles.Info),
            ResourceType = "Neon",
            Properties =
            [
                new(CustomResourceKnownProperties.Source, "Neon API"),
                new("Project", resource.Options.ProjectId ?? resource.Options.ProjectName ?? string.Empty),
                new("Branch", resource.Options.Branch.BranchId ?? resource.Options.Branch.BranchName ?? string.Empty),
            ],
        };

        builder.Services.AddHealthChecks()
            .AddTypeActivatedCheck<NeonHealthCheck>(healthCheckKey, resource);

        var resourceBuilder = builder.AddResource(resource)
            .WithInitialState(initialState)
            .WithHealthCheck(healthCheckKey)
            .ExcludeFromManifest();

        _ = resourceBuilder.EnsureProvisioner();

        object provisionLock = new();
        Task? provisioningTask = null;

        if (resource.ProvisionerResource is IResource provisionerResource)
        {
            builder
                .Eventing
                .Subscribe<ResourceReadyEvent>(provisionerResource, (@event, ct) =>
                {
                    lock (provisionLock)
                    {
                        if (!resource.TryGetLastAnnotation<NeonExternalProvisionerAnnotation>(out var provisionerAnnotation))
                        {
                            throw new DistributedApplicationException(
                                "Neon provisioner annotation is missing. Ensure a Neon provisioner resource is configured.");
                        }

                        provisioningTask ??= ConfigureNeonConnectionWithErrorHandlingAsync(
                            builder,
                            resource,
                            provisionerAnnotation,
                            @event.Services,
                            CancellationToken.None);
                        return provisioningTask;
                    }
                });
        }

        resourceBuilder
            .WithCommand(
                "neon-suspend",
                "Suspend",
                async context =>
                {
                    if (string.IsNullOrWhiteSpace(resource.ProjectId) ||
                        string.IsNullOrWhiteSpace(resource.EndpointId))
                    {
                        return new ExecuteCommandResult { Success = false, ErrorMessage = "Resource is not provisioned." };
                    }

                    if (!resource.TryGetLastAnnotation<NeonExternalProvisionerAnnotation>(out var provisionerAnnotation))
                    {
                        return new ExecuteCommandResult { Success = false, ErrorMessage = "Provisioner is not configured for this Neon resource." };
                    }

                    try
                    {
                        await ExecuteProvisionerEndpointCommandAsync(
                            provisionerAnnotation,
                            resource,
                            "suspend",
                            context.CancellationToken).ConfigureAwait(false);

                        var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
                        var suspendLogger = context.ServiceProvider.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
                        suspendLogger.LogInformation("Neon compute endpoint suspended.");

                        await notificationService.PublishUpdateAsync(resource, state => state with
                        {
                            State = new("Suspended", KnownResourceStateStyles.Info),
                            StopTimeStamp = DateTime.UtcNow,
                        }).ConfigureAwait(false);

                        return new ExecuteCommandResult { Success = true };
                    }
                    catch (Exception ex)
                    {
                        return new ExecuteCommandResult { Success = false, ErrorMessage = ex.Message };
                    }
                },
                new CommandOptions
                {
                    IconName = "Stop",
                    IconVariant = IconVariant.Filled,
                    IsHighlighted = true,
                    Description = "Suspend the Neon compute endpoint. Free plan computes auto-suspend after 5 minutes of inactivity.",
                    UpdateState = ctx =>
                    {
                        var stateText = ctx.ResourceSnapshot.State?.Text;
                        return stateText == KnownResourceStates.Running
                            ? ResourceCommandState.Enabled
                            : ResourceCommandState.Hidden;
                    },
                })
            .WithCommand(
                "neon-resume",
                "Resume",
                async context =>
                {
                    if (string.IsNullOrWhiteSpace(resource.ProjectId) ||
                        string.IsNullOrWhiteSpace(resource.EndpointId))
                    {
                        return new ExecuteCommandResult { Success = false, ErrorMessage = "Resource is not provisioned." };
                    }

                    if (!resource.TryGetLastAnnotation<NeonExternalProvisionerAnnotation>(out var provisionerAnnotation))
                    {
                        return new ExecuteCommandResult { Success = false, ErrorMessage = "Provisioner is not configured for this Neon resource." };
                    }

                    try
                    {
                        await ExecuteProvisionerEndpointCommandAsync(
                            provisionerAnnotation,
                            resource,
                            "resume",
                            context.CancellationToken).ConfigureAwait(false);

                        var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();
                        var resumeLogger = context.ServiceProvider.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
                        resumeLogger.LogInformation("Neon compute endpoint resumed.");

                        await notificationService.PublishUpdateAsync(resource, state => state with
                        {
                            State = new(KnownResourceStates.Running, null),
                            StartTimeStamp = DateTime.UtcNow,
                        }).ConfigureAwait(false);

                        return new ExecuteCommandResult { Success = true };
                    }
                    catch (Exception ex)
                    {
                        return new ExecuteCommandResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                        };
                    }
                },
                new CommandOptions
                {
                    IconName = "Play",
                    IconVariant = IconVariant.Filled,
                    IsHighlighted = true,
                    Description = "Resume the suspended Neon compute endpoint.",
                    UpdateState = ctx =>
                        ctx.ResourceSnapshot.State?.Text == "Suspended"
                            ? ResourceCommandState.Enabled
                            : ResourceCommandState.Hidden,
                });

        return resourceBuilder;
    }

    /// <summary>
    /// Adds a Neon database resource to the application model.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="databaseName">The database name. Defaults to <paramref name="name"/>.</param>
    /// <param name="roleName">The role name to use for connections. Defaults to <c>{database}_owner</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NeonDatabaseResource> AddDatabase(
        this IResourceBuilder<NeonProjectResource> builder,
        [ResourceName] string name,
        string? databaseName = null,
        string? roleName = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        databaseName ??= name;
        roleName = ResolveRoleName(roleName, databaseName, builder.Resource.Options.RoleName);

        NeonDatabaseResource databaseResource = new(name, databaseName, roleName, builder.Resource);
        builder.Resource.AddDatabase(databaseResource);

        if (builder.Resource.ProvisionerResource is not null)
        {
            _ = builder.EnsureProvisioner();
        }

        var dbHealthCheckKey = $"{name}_neon_db_health";

        builder.ApplicationBuilder.Services.AddHealthChecks()
            .AddTypeActivatedCheck<NeonDatabaseHealthCheck>(dbHealthCheckKey, databaseResource);

        return builder
            .ApplicationBuilder
            .AddResource(databaseResource)
            .WithInitialState(new CustomResourceSnapshot
            {
                State = new(KnownResourceStates.Starting, KnownResourceStateStyles.Info),
                ResourceType = "Neon Database",
                Properties =
                [
                    new("Database", databaseName),
                    new("Role", roleName),
                ],
            })
            .WithHealthCheck(dbHealthCheckKey)
            .WithParentRelationship(builder.Resource);
    }

    private static string ResolveRoleName(
        string? roleName,
        string databaseName,
        string? defaultRoleName
    )
    {
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            return roleName;
        }

        if (!string.IsNullOrWhiteSpace(defaultRoleName))
        {
            return defaultRoleName;
        }

        return $"{databaseName}_owner";
    }

    private static async Task ConfigureNeonConnectionFromOutputAsync(
        IDistributedApplicationBuilder builder,
        NeonProjectResource resource,
        NeonExternalProvisionerAnnotation provisionerAnnotation,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var notificationService = services.GetRequiredService<ResourceNotificationService>();
        var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);

        logger.LogInformation("Loading Neon provisioner output from {OutputPath} in {Mode} mode.", provisionerAnnotation.OutputFilePath, provisionerAnnotation.Mode);

        NeonProvisionerOutput output = await ReadProvisionerOutputAsync(
            provisionerAnnotation.OutputFilePath,
            logger,
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Neon provisioner output loaded. Project={ProjectId}, Branch={BranchId}, Endpoint={EndpointId}, Databases={DatabaseCount}",
            output.ProjectId,
            output.BranchId,
            output.EndpointId,
            output.Databases.Count);

        var defaultConnectionInfo = NeonConnectionInfo.Parse(output.DefaultConnectionUri);

        resource.ProjectId = output.ProjectId;
        resource.BranchId = output.BranchId;
        resource.EndpointId = output.EndpointId;
        resource.ConnectionUri = output.DefaultConnectionUri;
        resource.DatabaseName = output.DefaultDatabaseName;
        resource.RoleName = output.DefaultRoleName;
        resource.Host = output.Host ?? defaultConnectionInfo.Host;
        resource.Port = output.Port ?? defaultConnectionInfo.Port;
        resource.Password = output.Password ?? defaultConnectionInfo.Password;

        var databaseResources = builder.Resources.OfType<NeonDatabaseResource>()
            .Where(database => database.Parent == resource)
            .ToList();

        foreach (var database in databaseResources)
        {
            string availableDatabases = output.Databases.Count == 0
                ? "<none>"
                : string.Join(", ", output.Databases.Select(item => $"{item.ResourceName}:{item.DatabaseName}/{item.RoleName}"));

            var outputDatabase = output.Databases.FirstOrDefault(item =>
                string.Equals(item.ResourceName, database.Name, StringComparison.OrdinalIgnoreCase))
                ?? output.Databases.FirstOrDefault(item =>
                    string.Equals(item.DatabaseName, database.DatabaseName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.RoleName, database.RoleName, StringComparison.OrdinalIgnoreCase));

            if (outputDatabase is null)
            {
                throw new DistributedApplicationException(
                    $"Neon provisioner output did not include database '{database.Name}' ({database.DatabaseName}/{database.RoleName}). Available databases: {availableDatabases}.");
            }

            var connectionInfo = NeonConnectionInfo.Parse(outputDatabase.ConnectionUri);
            database.ConnectionUri = outputDatabase.ConnectionUri;
            database.Host = outputDatabase.Host ?? connectionInfo.Host;
            database.Port = outputDatabase.Port ?? connectionInfo.Port;
            database.Password = outputDatabase.Password ?? connectionInfo.Password;

            logger.LogInformation(
                "Configured Neon database resource. Resource={ResourceName}, Database={DatabaseName}, Role={RoleName}, Host={Host}, Port={Port}",
                database.Name,
                database.DatabaseName,
                database.RoleName,
                database.Host,
                database.Port);
        }

        var consoleUrl = $"https://console.neon.tech/app/projects/{resource.ProjectId}/branches/{resource.BranchId}";
        var resourceProperties = new List<ResourcePropertySnapshot>
        {
            new(CustomResourceKnownProperties.Source, "Neon"),
            new("Project", resource.ProjectId ?? string.Empty),
            new("Branch", resource.BranchId ?? string.Empty),
        };

        if (!string.IsNullOrWhiteSpace(resource.Host))
        {
            resourceProperties.Add(new("Host", resource.Host));
        }

        if (!string.IsNullOrWhiteSpace(output.EndpointRegionId))
        {
            resourceProperties.Add(new("Region", output.EndpointRegionId));
        }

        if (!string.IsNullOrWhiteSpace(output.EndpointType))
        {
            resourceProperties.Add(new("EndpointType", output.EndpointType));
        }

        if (output.EndpointSuspendTimeoutSeconds.HasValue && output.EndpointSuspendTimeoutSeconds.Value > 0)
        {
            resourceProperties.Add(new("AutoSuspend", $"{output.EndpointSuspendTimeoutSeconds.Value}s"));
        }

        await notificationService.PublishUpdateAsync(resource, state => state with
        {
            State = new(KnownResourceStates.Running, null),
            StartTimeStamp = DateTime.UtcNow,
            Urls = [new UrlSnapshot("Neon Console", consoleUrl, IsInternal: false)],
            Properties = MergeProperties(state.Properties, resourceProperties),
        }).ConfigureAwait(false);

        foreach (var database in databaseResources)
        {
            var databaseConsoleUrl = $"{consoleUrl}/tables?database={Uri.EscapeDataString(database.DatabaseName)}";
            List<ResourcePropertySnapshot> databaseProperties =
            [
                new(CustomResourceKnownProperties.Source, database.DatabaseName),
                new("Database", database.DatabaseName),
                new("Role", database.RoleName),
                new("Host", database.Host ?? string.Empty),
            ];

            await notificationService.PublishUpdateAsync(database, state => state with
            {
                State = new(KnownResourceStates.Running, null),
                StartTimeStamp = DateTime.UtcNow,
                Urls = [new UrlSnapshot("Neon Console", databaseConsoleUrl, IsInternal: false)],
                Properties = MergeProperties(state.Properties, databaseProperties),
            }).ConfigureAwait(false);
        }

        logger.LogInformation(
            "Neon {Mode} completed from external provisioner output. Project={ProjectId}, Branch={BranchId}",
            provisionerAnnotation.Mode,
            resource.ProjectId,
            resource.BranchId);
    }

    private static async Task ConfigureNeonConnectionWithErrorHandlingAsync(
        IDistributedApplicationBuilder builder,
        NeonProjectResource resource,
        NeonExternalProvisionerAnnotation provisionerAnnotation,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var notificationService = services.GetRequiredService<ResourceNotificationService>();
        var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);

        try
        {
            await ConfigureNeonConnectionFromOutputAsync(
                builder,
                resource,
                provisionerAnnotation,
                services,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure Neon resource from provisioner output.");

            await notificationService.PublishUpdateAsync(resource, state => state with
            {
                State = new(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error),
            }).ConfigureAwait(false);

            throw;
        }
    }

    private static async Task<NeonProvisionerOutput> ReadProvisionerOutputAsync(
        string filePath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMinutes(2);
        var retryDelay = TimeSpan.FromMilliseconds(500);
        var deadline = DateTime.UtcNow + timeout;
        string failureFilePath = $"{filePath}.error.log";
        string? lastObservation = null;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(failureFilePath))
            {
                string errorText = await File.ReadAllTextAsync(failureFilePath, cancellationToken).ConfigureAwait(false);
                logger.LogError(
                    "Neon provisioner failure artifact detected at {FailurePath}.",
                    failureFilePath);

                throw new DistributedApplicationException(
                    $"Neon provisioner failed before producing output. See 'neon-provisioner' logs. Details: {errorText}");
            }

            if (!File.Exists(filePath))
            {
                lastObservation = "Output file not found yet.";
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string json;
            try
            {
                json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                lastObservation = $"Output file is temporarily unavailable: {ex.Message}";
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                lastObservation = "Output file exists but is empty.";
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                var output = JsonSerializer.Deserialize<NeonProvisionerOutput>(json);
                if (output is not null)
                {
                    return output;
                }

                lastObservation = "Output JSON deserialized to null.";
            }
            catch (JsonException ex)
            {
                lastObservation = $"Output JSON is not valid yet: {ex.Message}";
            }

            await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
        }

        logger.LogError(
            "Timed out waiting for Neon provisioner output file {OutputPath}. Last observation: {Observation}",
            filePath,
            lastObservation ?? "No output observed.");

        throw new DistributedApplicationException(
            $"Neon provisioner output file '{filePath}' was not produced in time or contained invalid JSON. Last observation: {lastObservation ?? "No output observed."} Check the 'neon-provisioner' resource logs for the underlying error.");
    }

    private static ImmutableArray<ResourcePropertySnapshot> MergeProperties(
        IReadOnlyList<ResourcePropertySnapshot>? existingProperties,
        IReadOnlyList<ResourcePropertySnapshot> updates)
    {
        Dictionary<string, ResourcePropertySnapshot> merged = new(StringComparer.OrdinalIgnoreCase);

        if (existingProperties is not null)
        {
            foreach (ResourcePropertySnapshot property in existingProperties)
            {
                merged[property.Name] = property;
            }
        }

        foreach (ResourcePropertySnapshot property in updates)
        {
            merged[property.Name] = property;
        }

        return [.. merged.Values];
    }

    private static async Task ExecuteProvisionerEndpointCommandAsync(
        NeonExternalProvisionerAnnotation annotation,
        NeonProjectResource resource,
        string mode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(annotation.ProjectPath) || !File.Exists(annotation.ProjectPath))
        {
            throw new DistributedApplicationException(
                $"Unable to run provisioner operation because project path '{annotation.ProjectPath}' was not found.");
        }

        var apiKey = await resource.ApiKeyParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new DistributedApplicationException("Neon API key is required but was not provided.");
        }

        var processStartInfo = new ProcessStartInfo("dotnet", $"run --project \"{annotation.ProjectPath}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        processStartInfo.Environment[NeonProvisionerEnvironmentVariables.ApiKey] = apiKey;
        processStartInfo.Environment[NeonProvisionerEnvironmentVariables.Mode] = mode;
        processStartInfo.Environment[NeonProvisionerEnvironmentVariables.ProjectId] = resource.ProjectId;
        processStartInfo.Environment[NeonProvisionerEnvironmentVariables.EndpointId] = resource.EndpointId;

        using var process = Process.Start(processStartInfo)
            ?? throw new DistributedApplicationException("Failed to launch Neon provisioner process.");

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(standardError) ? standardOutput : standardError;
            throw new DistributedApplicationException(
                $"Neon provisioner {mode} operation failed with exit code {process.ExitCode}. {details}".Trim());
        }
    }

}
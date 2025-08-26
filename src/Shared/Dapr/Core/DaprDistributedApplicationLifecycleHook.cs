// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;
using System.Threading.Tasks;
using static CommunityToolkit.Aspire.Hosting.Dapr.CommandLineArgs;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

internal sealed class DaprDistributedApplicationLifecycleHook(
    IDaprPublishingHelper publishingHelper,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DaprDistributedApplicationLifecycleHook> logger,
    IOptions<DaprOptions> options) : IDistributedApplicationLifecycleHook, IDisposable
{
    private readonly DaprOptions _options = options.Value;

    private string? _onDemandResourcesRootPath;

    public async Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        string appHostDirectory = GetAppHostDirectory();

        var onDemandResourcesPaths = await StartOnDemandDaprComponentsAsync(appModel, cancellationToken).ConfigureAwait(false);

        var sideCars = new List<ExecutableResource>();


        foreach (var resource in appModel.Resources)
        {

            if (!resource.TryGetLastAnnotation<DaprSidecarAnnotation>(out var daprAnnotation))
            {
                continue;
            }

            var fileName = _options.DaprPath
                ?? GetDefaultDaprPath()
                ?? throw new DistributedApplicationException("Unable to locate the Dapr CLI.");

            var daprSidecar = daprAnnotation.Sidecar;


            var sidecarOptionsAnnotation = daprSidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().LastOrDefault();

            var sidecarOptions = sidecarOptionsAnnotation?.Options;

            [return: NotNullIfNotNull(nameof(path))]
            string? NormalizePath(string? path)
            {
                if (path is null)
                {
                    return null;
                }

                return Path.GetFullPath(Path.Combine(appHostDirectory, path));
            }

            var aggregateResourcesPaths = sidecarOptions?.ResourcesPaths.Select(path => NormalizePath(path)).ToHashSet() ?? [];

            var componentReferenceAnnotations = resource.Annotations.OfType<DaprComponentReferenceAnnotation>();

            var waitAnnotationsToCopyToDaprCli = new List<WaitAnnotation>();

            var secrets = new Dictionary<string, string>();
            var endpointEnvironmentVars = new Dictionary<string, IValueProvider>();
            var hasValueProviders = false;

            foreach (var componentReferenceAnnotation in componentReferenceAnnotations)
            {
                // Check if there are any value provider references that need to be added as environment variables
                if (componentReferenceAnnotation.Component.TryGetAnnotationsOfType<DaprComponentValueProviderAnnotation>(out var endpointAnnotations))
                {
                    foreach (var endpointAnnotation in endpointAnnotations)
                    {
                        endpointEnvironmentVars[endpointAnnotation.EnvironmentVariableName] = endpointAnnotation.ValueProvider;
                        hasValueProviders = true;
                    }
                }
                
                // Check if there are any secrets that need to be added to the secret store
                if (componentReferenceAnnotation.Component.TryGetAnnotationsOfType<DaprComponentSecretAnnotation>(out var secretAnnotations))
                {
                    foreach (var secretAnnotation in secretAnnotations)
                    {
                        secrets[secretAnnotation.Key] = (await secretAnnotation.Value.GetValueAsync(cancellationToken))!;
                    }
                }
                
                // If we have any secrets or value providers, ensure the secret store path is added
                if ((secrets.Count > 0 || hasValueProviders) && onDemandResourcesPaths.TryGetValue("secretstore", out var secretStorePath))
                {
                    string onDemandResourcesPathDirectory = Path.GetDirectoryName(secretStorePath)!;
                    if (onDemandResourcesPathDirectory is not null)
                    {
                        aggregateResourcesPaths.Add(onDemandResourcesPathDirectory);
                    }
                }

                // Whilst we are passing over each component annotations collect the list of annotations to copy to the Dapr CLI.
                if (componentReferenceAnnotation.Component.TryGetAnnotationsOfType<WaitAnnotation>(out var componentWaitAnnotations))
                {
                    waitAnnotationsToCopyToDaprCli.AddRange(componentWaitAnnotations);
                }

                if (componentReferenceAnnotation.Component.Options?.LocalPath is not null)
                {
                    var localPathDirectory = Path.GetDirectoryName(NormalizePath(componentReferenceAnnotation.Component.Options.LocalPath));

                    if (localPathDirectory is not null)
                    {
                        aggregateResourcesPaths.Add(localPathDirectory);
                    }
                }
                else if (onDemandResourcesPaths.TryGetValue(componentReferenceAnnotation.Component.Name, out var onDemandResourcesPath))
                {
                    string onDemandResourcesPathDirectory = Path.GetDirectoryName(onDemandResourcesPath)!;

                    if (onDemandResourcesPathDirectory is not null)
                    {
                        aggregateResourcesPaths.Add(onDemandResourcesPathDirectory);
                    }
                }
            }

            if (secrets.Count > 0 || endpointEnvironmentVars.Count > 0)
            {
                daprSidecar.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
                {
                    foreach (var secret in secrets)
                    {
                        context.EnvironmentVariables.TryAdd(secret.Key, secret.Value);
                    }
                    
                    // Add value provider references
                    foreach (var (envVarName, valueProvider) in endpointEnvironmentVars)
                    {
                        var value = await valueProvider.GetValueAsync(context.CancellationToken);
                        context.EnvironmentVariables.TryAdd(envVarName, value ?? string.Empty);
                    }
                }));
            }
            // It is possible that we have duplicate wate annotations so we just dedupe them here.
            var distinctWaitAnnotationsToCopyToDaprCli = waitAnnotationsToCopyToDaprCli.DistinctBy(w => (w.Resource, w.WaitType));

            var daprAppPortArg = (int? port) => ModelNamedArg("--app-port", port);
            var daprGrpcPortArg = (object port) => ModelNamedObjectArg("--dapr-grpc-port", port);
            var daprHttpPortArg = (object port) => ModelNamedObjectArg("--dapr-http-port", port);
            var daprMetricsPortArg = (object port) => ModelNamedObjectArg("--metrics-port", port);
            var daprProfilePortArg = (object port) => ModelNamedObjectArg("--profile-port", port);
            var daprAppChannelAddressArg = (string? address) => ModelNamedArg("--app-channel-address", address);
            var daprAppProtocol = (string? protocol) => ModelNamedArg("--app-protocol", protocol);

            var appId = sidecarOptions?.AppId ?? resource.Name;

#pragma warning disable CS0618 // Type or member is obsolete
            string? maxBodySize = GetValueIfSet(sidecarOptions?.DaprMaxBodySize, sidecarOptions?.DaprHttpMaxRequestSize, "Mi");
            string? readBufferSize = GetValueIfSet(sidecarOptions?.DaprReadBufferSize, sidecarOptions?.DaprHttpReadBufferSize, "Ki");
#pragma warning restore CS0618 // Type or member is obsolete

            var daprCommandLine =
                CommandLineBuilder
                    .Create(
                        fileName,
                        Command("run"),
                        daprAppPortArg(sidecarOptions?.AppPort),
                        ModelNamedArg("--app-channel-address", sidecarOptions?.AppChannelAddress),
                        ModelNamedArg("--app-health-check-path", sidecarOptions?.AppHealthCheckPath),
                        ModelNamedArg("--app-health-probe-interval", sidecarOptions?.AppHealthProbeInterval),
                        ModelNamedArg("--app-health-probe-timeout", sidecarOptions?.AppHealthProbeTimeout),
                        ModelNamedArg("--app-health-threshold", sidecarOptions?.AppHealthThreshold),
                        ModelNamedArg("--app-id", appId),
                        ModelNamedArg("--app-max-concurrency", sidecarOptions?.AppMaxConcurrency),
                        ModelNamedArg("--app-protocol", sidecarOptions?.AppProtocol),
                        ModelNamedArg("--config", NormalizePath(sidecarOptions?.Config)),
                        ModelNamedArg("--max-body-size", sidecarOptions?.DaprMaxBodySize),
                        ModelNamedArg("--read-buffer-size", sidecarOptions?.DaprReadBufferSize),
                        ModelNamedArg("--dapr-internal-grpc-port", sidecarOptions?.DaprInternalGrpcPort),
                        ModelNamedArg("--dapr-listen-addresses", sidecarOptions?.DaprListenAddresses),
                        Flag("--enable-api-logging", sidecarOptions?.EnableApiLogging),
                        Flag("--enable-app-health-check", sidecarOptions?.EnableAppHealthCheck),
                        Flag("--enable-profiling", sidecarOptions?.EnableProfiling),
                        ModelNamedArg("--log-level", sidecarOptions?.LogLevel),
                        ModelNamedArg("--placement-host-address", sidecarOptions?.PlacementHostAddress),
                        ModelNamedArg("--resources-path", aggregateResourcesPaths),
                        ModelNamedArg("--run-file", NormalizePath(sidecarOptions?.RunFile)),
                        ModelNamedArg("--runtime-path", NormalizePath(sidecarOptions?.RuntimePath)),
                        ModelNamedArg("--scheduler-host-address", sidecarOptions?.SchedulerHostAddress),
                        ModelNamedArg("--unix-domain-socket", sidecarOptions?.UnixDomainSocket),
                        PostOptionsArgs(Args(sidecarOptions?.Command)));

            var daprCliResourceName = $"{daprSidecar.Name}-cli";
            var daprCli = new ExecutableResource(daprCliResourceName, fileName, appHostDirectory);

            // Add all the unique wait annotations to the CLI.
            daprCli.Annotations.AddRange(distinctWaitAnnotationsToCopyToDaprCli);

            resource.Annotations.Add(
                new EnvironmentCallbackAnnotation(
                    context =>
                    {
                        if (context.ExecutionContext.IsPublishMode)
                        {
                            return;
                        }

                        var http = daprCli.GetEndpoint("http");
                        var grpc = daprCli.GetEndpoint("grpc");

                        context.EnvironmentVariables.TryAdd("DAPR_HTTP_PORT", http.Port.ToString(CultureInfo.InvariantCulture));
                        context.EnvironmentVariables.TryAdd("DAPR_GRPC_PORT", grpc.Port.ToString(CultureInfo.InvariantCulture));

                        context.EnvironmentVariables.TryAdd("DAPR_GRPC_ENDPOINT", grpc);
                        context.EnvironmentVariables.TryAdd("DAPR_HTTP_ENDPOINT", http);

                    }));

            daprCli.Annotations.Add(new EndpointAnnotation(ProtocolType.Tcp, uriScheme: "http", name: "grpc", port: sidecarOptions?.DaprGrpcPort));
            daprCli.Annotations.Add(new EndpointAnnotation(ProtocolType.Tcp, uriScheme: "http", name: "http", port: sidecarOptions?.DaprHttpPort));
            daprCli.Annotations.Add(new EndpointAnnotation(ProtocolType.Tcp, uriScheme: "http", name: "metrics", port: sidecarOptions?.MetricsPort));
            if (sidecarOptions?.EnableProfiling == true)
            {
                daprCli.Annotations.Add(new EndpointAnnotation(ProtocolType.Tcp, name: "profile", port: sidecarOptions?.ProfilePort, uriScheme: "http"));
            }

            // NOTE: Telemetry is enabled by default.
            if (_options.EnableTelemetry != false)
            {
                OtlpConfigurationExtensions.AddOtlpEnvironment(daprCli, configuration, environment);
            }

            daprCli.Annotations.Add(
                new CommandLineArgsCallbackAnnotation(
                    updatedArgs =>
                    {
                        updatedArgs.AddRange(daprCommandLine.Arguments);
                        var endPoint = GetEndpointReference(sidecarOptions, resource);

                        if (sidecarOptions?.AppPort is null && endPoint is { appEndpoint.IsAllocated: true })
                        {
                            updatedArgs.AddRange(daprAppPortArg(endPoint.Value.appEndpoint.Port)());
                        }

                        var grpc = daprCli.GetEndpoint("grpc");
                        var http = daprCli.GetEndpoint("http");
                        var metrics = daprCli.GetEndpoint("metrics");

                        updatedArgs.AddRange(daprGrpcPortArg(grpc.Property(EndpointProperty.TargetPort))());
                        updatedArgs.AddRange(daprHttpPortArg(http.Property(EndpointProperty.TargetPort))());
                        updatedArgs.AddRange(daprMetricsPortArg(metrics.Property(EndpointProperty.TargetPort))());

                        if (sidecarOptions?.EnableProfiling == true)
                        {
                            var profiling = daprCli.GetEndpoint("profiling");

                            updatedArgs.AddRange(daprProfilePortArg(profiling.Property(EndpointProperty.TargetPort))());
                        }

                        if (sidecarOptions?.AppChannelAddress is null && endPoint is { appEndpoint.IsAllocated: true })
                        {
                            updatedArgs.AddRange(daprAppChannelAddressArg(endPoint.Value.appEndpoint.Host)());
                        }
                        if (sidecarOptions?.AppProtocol is null && endPoint is { appEndpoint.IsAllocated: true })
                        {
                            updatedArgs.AddRange(daprAppProtocol(endPoint.Value.protocol)());
                        }
                    }));

            // Apply environment variables to the CLI...
            daprCli.Annotations.AddRange(daprSidecar.Annotations.OfType<EnvironmentCallbackAnnotation>());

            // The CLI is an artifact of a local run, so it should not be published...
            daprCli.Annotations.Add(ManifestPublishingCallbackAnnotation.Ignore);

            // https://github.com/CommunityToolkit/Aspire/issues/507
            // The CLI should be a child of the resource that it is associated with.
            daprCli.Annotations.Add(new ResourceRelationshipAnnotation(resource, "Parent"));

            daprSidecar.Annotations.Add(
                new ManifestPublishingCallbackAnnotation(
                    context =>
                    {
                        context.Writer.WriteString("type", "dapr.v0");
                        context.Writer.WriteStartObject("dapr");

                        context.Writer.WriteString("application", resource.Name);
                        context.Writer.TryWriteString("appChannelAddress", sidecarOptions?.AppChannelAddress);
                        context.Writer.TryWriteString("appHealthCheckPath", sidecarOptions?.AppHealthCheckPath);
                        context.Writer.TryWriteNumber("appHealthProbeInterval", sidecarOptions?.AppHealthProbeInterval);
                        context.Writer.TryWriteNumber("appHealthProbeTimeout", sidecarOptions?.AppHealthProbeTimeout);
                        context.Writer.TryWriteNumber("appHealthThreshold", sidecarOptions?.AppHealthThreshold);
                        context.Writer.TryWriteString("appId", appId);
                        context.Writer.TryWriteNumber("appMaxConcurrency", sidecarOptions?.AppMaxConcurrency);
                        context.Writer.TryWriteNumber("appPort", sidecarOptions?.AppPort);
                        context.Writer.TryWriteString("appProtocol", sidecarOptions?.AppProtocol);
                        context.Writer.TryWriteStringArray("command", sidecarOptions?.Command);
                        context.Writer.TryWriteStringArray("components", componentReferenceAnnotations.Select(componentReferenceAnnotation => componentReferenceAnnotation.Component.Name));
                        context.Writer.TryWriteString("config", context.GetManifestRelativePath(sidecarOptions?.Config));
                        context.Writer.TryWriteNumber("daprGrpcPort", sidecarOptions?.DaprGrpcPort);
                        context.Writer.TryWriteString("daprMaxBodySize", sidecarOptions?.DaprMaxBodySize);
                        context.Writer.TryWriteNumber("daprHttpPort", sidecarOptions?.DaprHttpPort);
                        context.Writer.TryWriteString("daprReadBufferSize", sidecarOptions?.DaprReadBufferSize);
                        context.Writer.TryWriteNumber("daprInternalGrpcPort", sidecarOptions?.DaprInternalGrpcPort);
                        context.Writer.TryWriteString("daprListenAddresses", sidecarOptions?.DaprListenAddresses);
                        context.Writer.TryWriteBoolean("enableApiLogging", sidecarOptions?.EnableApiLogging);
                        context.Writer.TryWriteBoolean("enableAppHealthCheck", sidecarOptions?.EnableAppHealthCheck);
                        context.Writer.TryWriteString("logLevel", sidecarOptions?.LogLevel);
                        context.Writer.TryWriteNumber("metricsPort", sidecarOptions?.MetricsPort);
                        context.Writer.TryWriteString("placementHostAddress", sidecarOptions?.PlacementHostAddress);
                        context.Writer.TryWriteNumber("profilePort", sidecarOptions?.ProfilePort);
                        context.Writer.TryWriteStringArray("resourcesPath", sidecarOptions?.ResourcesPaths.Select(path => context.GetManifestRelativePath(path)));
                        context.Writer.TryWriteString("runFile", context.GetManifestRelativePath(sidecarOptions?.RunFile));
                        context.Writer.TryWriteString("runtimePath", context.GetManifestRelativePath(sidecarOptions?.RuntimePath));
                        context.Writer.TryWriteString("schedulerHostAddress", sidecarOptions?.SchedulerHostAddress);
                        context.Writer.TryWriteString("unixDomainSocket", sidecarOptions?.UnixDomainSocket);
                        context.Writer.WriteEndObject();
                    }));


            await publishingHelper.ExecuteProviderSpecificRequirements(appModel, resource, sidecarOptions, cancellationToken);

            sideCars.Add(daprCli);
        }


        appModel.Resources.AddRange(sideCars);
    }

    private static string? GetValueIfSet(string? newValue, int? obsoleteValue, string notation)
    {
        if (newValue is not null) return newValue;
        if (obsoleteValue is not null) return $"{obsoleteValue}{notation}";
        return null;
    }

    private string GetAppHostDirectory() =>
        configuration["AppHost:Directory"]
        ?? throw new InvalidOperationException("Unable to obtain the application host directory.");


    // This method resolves the application's endpoint and the protocol that the dapr side car will use.
    // It depends on DaprSidecarOptions.AppProtocol and DaprSidecarOptions.AppEndpoint.
    // - If both are null default to 'http' for both.
    // - If AppProtocol is not null try to get an endpoint with the name of the protocol.
    // - if AppEndpoint is not null try to use the scheme as the protocol.
    // - if both are not null just use both options.
    static (EndpointReference appEndpoint, string protocol)? GetEndpointReference(DaprSidecarOptions? sidecarOptions, IResource resource)
    {
        if (resource is IResourceWithEndpoints resourceWithEndpoints)
        {
            return (sidecarOptions?.AppProtocol, sidecarOptions?.AppEndpoint) switch
            {
                (null, null) => (resourceWithEndpoints.GetEndpoint("http"), "http"),
                (null, string appEndpoint) => (resourceWithEndpoints.GetEndpoint(appEndpoint), resourceWithEndpoints.GetEndpoint(appEndpoint).Scheme),
                (string appProtocol, null) => (resourceWithEndpoints.GetEndpoint(appProtocol), appProtocol),
                (string appProtocol, string appEndpoint) => (resourceWithEndpoints.GetEndpoint(appEndpoint), appProtocol)
            };
        }
        return null;
    }

    /// <summary>
    /// Return the first verified dapr path
    /// </summary>
    static string? GetDefaultDaprPath()
    {
        foreach (var path in GetAvailablePaths())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return default;

        // Return all the possible paths for dapr
        static IEnumerable<string> GetAvailablePaths()
        {
            if (OperatingSystem.IsWindows())
            {
                var pathRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:";

                // Installed windows paths:
                yield return Path.Combine(pathRoot, "dapr", "dapr.exe");

                // Add all the paths that are reachable via the `PATH` environment variable:
                var possibleWindowsDaprPaths = Environment.GetEnvironmentVariable("PATH")?
                    .Split(Path.PathSeparator)
                    .Select(path => Path.Combine(path, "dapr.exe"))
                    .Where(File.Exists) ?? [];
                foreach (var path in possibleWindowsDaprPaths)
                {
                    yield return path;
                }

                yield break;
            }

            // Add $HOME/dapr path:
            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            yield return Path.Combine(homePath, "dapr", "dapr");

            // Linux & MacOS path:
            yield return "/usr/local/bin/dapr";

            // Arch Linux path:
            yield return "/usr/bin/dapr";

            // MacOS Homebrew path:
            if (OperatingSystem.IsMacOS() && Environment.GetEnvironmentVariable("HOMEBREW_PREFIX") is string homebrewPrefix)
            {
                yield return Path.Combine(homebrewPrefix, "bin", "dapr");
            }

            // Add all the paths that are reachable via the `PATH` environment variable:
            var possibleDaprPaths = Environment.GetEnvironmentVariable("PATH")?
                .Split(Path.PathSeparator)
                .Select(path => Path.Combine(path, "dapr"))
                .Where(File.Exists) ?? [];
            foreach (var path in possibleDaprPaths)
            {
                yield return path;
            }
        }
    }

    public void Dispose()
    {
        if (_onDemandResourcesRootPath is not null)
        {
            logger.LogInformation("Stopping Dapr-related resources...");

            try
            {
                Directory.Delete(_onDemandResourcesRootPath, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete temporary Dapr resources directory: {OnDemandResourcesRootPath}", _onDemandResourcesRootPath);
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> StartOnDemandDaprComponentsAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        var onDemandComponents =
            appModel
                .Resources
                .OfType<DaprComponentResource>()
                .Where(component => component.Options?.LocalPath is null)
                .ToList();

        // If any of the components have secrets or value provider references, we will add an on-demand secret store component.
        bool needsSecretStore = onDemandComponents.Any(component => 
            (component.TryGetAnnotationsOfType<DaprComponentSecretAnnotation>(out var secretAnnotations) && secretAnnotations.Any()) ||
            (component.TryGetAnnotationsOfType<DaprComponentValueProviderAnnotation>(out var valueProviderAnnotations) && valueProviderAnnotations.Any()));
        
        if (needsSecretStore)
        {
            onDemandComponents.Add(new DaprComponentResource("secretstore", DaprConstants.BuildingBlocks.SecretStore));
        }

        var onDemandResourcesPaths = new Dictionary<string, string>();

        if (onDemandComponents.Any())
        {
            logger.LogInformation("Starting Dapr-related resources...");

            _onDemandResourcesRootPath = Directory.CreateTempSubdirectory("aspire-dapr.").FullName;

            foreach (var component in onDemandComponents)
            {
                Func<string, Task<string>> contentWriter =
                    async content =>
                    {
                        logger.LogDebug("Creating on-demand configuration for component '{ComponentName}' with content: {content}.", component.Name, content);
                        string componentDirectory = Path.Combine(_onDemandResourcesRootPath, component.Name);

                        Directory.CreateDirectory(componentDirectory);

                        string componentPath = Path.Combine(componentDirectory, $"{component.Name}.yaml");

                        await File.WriteAllTextAsync(componentPath, content, cancellationToken).ConfigureAwait(false);

                        return componentPath;
                    };

                string componentPath = await (component.Type switch
                {
                    DaprConstants.BuildingBlocks.PubSub => GetBuildingBlockComponentAsync(component, contentWriter, "pubsub.in-memory", cancellationToken), // NOTE: In memory component can only be used within a single Dapr application.
                    DaprConstants.BuildingBlocks.StateStore => GetBuildingBlockComponentAsync(component, contentWriter, "state.in-memory", cancellationToken),
                    DaprConstants.BuildingBlocks.SecretStore => GetBuildingBlockComponentAsync(component, contentWriter, "secretstores.local.env", cancellationToken),
                    _ => GetComponentAsync(component, contentWriter, cancellationToken)
                }).ConfigureAwait(false);

                onDemandResourcesPaths.Add(component.Name, componentPath);
            }
        }

        return onDemandResourcesPaths;
    }

    private async Task<string> GetComponentAsync(DaprComponentResource component, Func<string, Task<string>> contentWriter, CancellationToken cancellationToken)
    {
        // We should try to read content from a known location (such as aspire root directory)
        logger.LogInformation("Unvalidated configuration {specType} for component '{ComponentName}'.", component.Type, component.Name);
        return await contentWriter(await GetDaprComponent(component, component.Type, cancellationToken)).ConfigureAwait(false);
    }
    private async Task<string> GetBuildingBlockComponentAsync(DaprComponentResource component, Func<string, Task<string>> contentWriter, string defaultProvider, CancellationToken cancellationToken)
    {
        // Start by trying to get the component from the app host directory
        string daprAppHostRelativePath = GetAppHostRelativePath(component.Type);

        if (File.Exists(daprAppHostRelativePath))
        {
            logger.LogInformation("Using apphost relative path for dapr component '{ComponentName}'.", component.Name);

            string newContent = await GetDefaultContent(component, daprAppHostRelativePath, cancellationToken).ConfigureAwait(false);

            return await contentWriter(newContent).ConfigureAwait(false);
        }

        // If the component is not found in the app host directory, try to get it from the default components directory
        string daprDefaultStorePath = GetDefaultComponentPath(component.Type);

        if (File.Exists(daprDefaultStorePath))
        {
            logger.LogInformation("Using default dapr path for component '{ComponentName}'.", component.Name);

            string newContent = await GetDefaultContent(component, daprDefaultStorePath, cancellationToken).ConfigureAwait(false);

            return await contentWriter(newContent).ConfigureAwait(false);
        }

        // If the component is not found in the default components directory, use the in-memory secret store
        logger.LogInformation("Using in-memory provider for dapr component '{ComponentName}'.", component.Name);

        var content = new DaprComponentSchema(component.Name, defaultProvider).ToString();
        return await contentWriter(content).ConfigureAwait(false);
    }

    private string GetAppHostRelativePath(string componentName)
    {
        string appHostDirectory = GetAppHostDirectory();
        string daprDefaultComponentsDirectory = Path.Combine(appHostDirectory, ".dapr", "components");
        return Path.Combine(daprDefaultComponentsDirectory, $"{componentName}.yaml");
    }

    private static string GetDefaultComponentPath(string componentName)
    {
        string userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string daprDefaultComponentsDirectory = Path.Combine(userDirectory, ".dapr", "components");
        return Path.Combine(daprDefaultComponentsDirectory, $"{componentName}.yaml");
    }
    private static async Task<string> GetDefaultContent(DaprComponentResource component, string defaultContentPath, CancellationToken cancellationToken)
    {
        string defaultContent = await File.ReadAllTextAsync(defaultContentPath, cancellationToken).ConfigureAwait(false);
        string yaml = defaultContent.Replace($"name: {component.Type}", $"name: {component.Name}");
        DaprComponentSchema content = DaprComponentSchema.FromYaml(yaml);
        await ConfigureDaprComponent(component, content, cancellationToken);
        await content.ResolveAllValuesAsync(cancellationToken);
        return content.ToString();
    }


    private static async Task<string> GetDaprComponent(DaprComponentResource component, string type, CancellationToken cancellationToken = default)
    {
        var content = new DaprComponentSchema(component.Name, type);
        await ConfigureDaprComponent(component, content, cancellationToken);
        await content.ResolveAllValuesAsync(cancellationToken);
        return content.ToString();
    }

    private static async Task ConfigureDaprComponent(DaprComponentResource component, DaprComponentSchema content, CancellationToken cancellationToken = default)
    {
        if (component.TryGetAnnotationsOfType<DaprComponentSecretAnnotation>(out var secrets) && secrets.Any())
        {
            content.Auth = new DaprComponentAuth { SecretStore = "secretstore" };
        }
        if (component.TryGetAnnotationsOfType<DaprComponentConfigurationAnnotation>(out var annotations))
        {
            foreach (var annotation in annotations)
            {
                await annotation.Configure(content, cancellationToken);
            }
        }
    }
}

internal static class IListExtensions
{
    public static void AddRange<T>(this IList<T> list, IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            list.Add(item);
        }
    }
}

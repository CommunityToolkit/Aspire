using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using System.Text.Json;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Ollama resources to the application model.
/// </summary>
public static partial class OllamaResourceBuilderExtensions
{
    /// <summary>
    /// Adds an Ollama container resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">An optional fixed port to bind to the Ollama container. This will be provided randomly by Aspire if not set.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> AddOllama(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var resource = new OllamaResource(name).AddOllamaDefaultCommands();
        return builder.AddResource(resource)
          .WithAnnotation(new ContainerImageAnnotation { Image = OllamaContainerImageTags.Image, Tag = OllamaContainerImageTags.Tag, Registry = OllamaContainerImageTags.Registry })
          .WithHttpEndpoint(port: port, targetPort: 11434, name: OllamaResource.OllamaEndpointName)
          .WithHttpHealthCheck("/");
    }
    
    /// <summary>
    /// Adds an Ollama executable resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">An optional fixed port to bind to the Ollama process. This will be provided randomly by Aspire if not set.</param>
    /// <param name="targetPort">An optional fixed port to run the Ollama process. This will default to 11434 if not set.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaExecutableResource> AddOllamaLocal(this IDistributedApplicationBuilder builder, [ResourceName] string name, int? port = null, int? targetPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        var resource = new OllamaExecutableResource(name, "ollama").AddOllamaDefaultCommands();
        return builder.AddResource(resource)
            .WithArgs(["serve"])
            .WithHttpEndpoint(port: port, targetPort: targetPort ?? OllamaExecutableResource.DefaultHttpPort, name: OllamaExecutableResource.OllamaEndpointName)
            .WithHttpHealthCheck("/")
            .WithEnvironment(context =>
            {
                if (context.EnvironmentVariables.ContainsKey("OLLAMA_HOST"))
                    return;
                
                var ollamaEndpoint = resource.GetEndpoint(OllamaExecutableResource.OllamaEndpointName);
                context.EnvironmentVariables["OLLAMA_HOST"] = ReferenceExpression.Create($"{ollamaEndpoint.Property(EndpointProperty.Scheme)}://{ollamaEndpoint.EndpointAnnotation.TargetHost}:{ollamaEndpoint.Property(EndpointProperty.TargetPort)}");
            });
    }

    /// <summary>
    /// Adds a data volume to the Ollama container.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaResource> WithDataVolume(this IResourceBuilder<OllamaResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "ollama"), "/root/.ollama", isReadOnly);
    }

    /// <summary>
    /// Adds GPU support to the Ollama container.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="vendor">The GPU vendor, defaults to Nvidia.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
    /// <remarks>
    /// This will add the right arguments to the container to enable GPU support as per <see href="https://github.com/ollama/ollama/blob/main/docs/docker.md" />.
    /// </remarks>
    public static IResourceBuilder<OllamaResource> WithGPUSupport(this IResourceBuilder<OllamaResource> builder, OllamaGpuVendor vendor = OllamaGpuVendor.Nvidia)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return vendor switch
        {
            OllamaGpuVendor.Nvidia => builder.WithContainerRuntimeArgs("--gpus", "all"),
            OllamaGpuVendor.AMD => builder.WithAMDGPUSupport(),
            _ => throw new ArgumentException("Invalid GPU vendor", nameof(vendor))
        };
    }

    private static IResourceBuilder<OllamaResource> WithAMDGPUSupport(this IResourceBuilder<OllamaResource> builder)
    {
        if (builder.Resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var containerAnnotation))
        {
            if (containerAnnotation.Tag?.EndsWith("rocm") == false)
            {
                containerAnnotation.Tag += "-rocm";
            }
        }
        return builder.WithContainerRuntimeArgs("--device", "/dev/kfd", "--device", "/dev/dri");
    }

    private static T AddOllamaDefaultCommands<T>(this T ollamaResource) where T : IOllamaResource
    {
        return ollamaResource
            .AddServerResourceCommand(
                name: "ListAllModels",
                displayName: "List All Models",
                executeCommand: async (ollamaResource, ollamaClient, logger, notificationService, ct) =>
                {
                    var models = await ollamaClient.ListLocalModelsAsync(ct);

                    if (!models.Any())
                    {
                        logger.LogInformation("No models found in the Ollama resource.");
                        return CommandResults.Success();
                    }

                    logger.LogInformation("Models: {Models}", models.ToJson());

                    return CommandResults.Success();
                },
                displayDescription: "List all models in the Ollama resource.",
                iconName: "AppsList"
            ).AddServerResourceCommand(
                name: "ListRunningModels",
                displayName: "List Running Models",
                executeCommand: async (ollamaResource, ollamaClient, logger, notificationService, ct) =>
                {
                    var models = await ollamaClient.ListRunningModelsAsync(ct);

                    if (!models.Any())
                    {
                        logger.LogInformation("No running models found in the Ollama resource.");
                        return CommandResults.Success();
                    }

                    logger.LogInformation("Running Models: {Models}", models.ToJson());

                    return CommandResults.Success();
                },
                displayDescription: "List all running models in the Ollama resource.",
                iconName: "AppsList"
            );
    }
    
    private static T AddServerResourceCommand<T>(
        this T ollamaResource,
        string name,
        string displayName,
        Func<IOllamaResource, IOllamaApiClient, ILogger, ResourceNotificationService, CancellationToken, Task<ExecuteCommandResult>> executeCommand,
        string? displayDescription,
        object? parameter = null,
        string? confirmationMessage = null,
        string? iconName = null,
        IconVariant? iconVariant = IconVariant.Filled,
        bool isHighlighted = false) where T : IOllamaResource
    {
        ollamaResource.Annotations.Add(new ResourceCommandAnnotation(
            name: name,
            displayName: displayName,
            updateState: context =>
                context.ResourceSnapshot.State?.Text == KnownResourceStates.Running ?
                    ResourceCommandState.Enabled :
                    ResourceCommandState.Disabled,
            executeCommand: async context =>
            {
                (var success, var endpoint) = await OllamaUtilities.TryGetEndpointAsync(ollamaResource, context.CancellationToken);

                if (!success || endpoint is null)
                {
                    return new ExecuteCommandResult { Success = false, ErrorMessage = "Invalid connection string" };
                }

                var ollamaClient = new OllamaApiClient(endpoint);
                var logger = context.ServiceProvider.GetRequiredService<ResourceLoggerService>().GetLogger(ollamaResource);
                var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();

                return await executeCommand(ollamaResource, ollamaClient, logger, notificationService, context.CancellationToken);
            },
            displayDescription: displayDescription,
            parameter: parameter,
            confirmationMessage: confirmationMessage,
            iconName: iconName,
            iconVariant: iconVariant,
            isHighlighted: isHighlighted
        ));

        return ollamaResource;
    }

    // this is a workaround since we can't write to the structured logs, we'll JSON print for now
    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
    private static string ToJson(this object obj)
    {
        return JsonSerializer.Serialize(obj, jsonSerializerOptions);
    }
}

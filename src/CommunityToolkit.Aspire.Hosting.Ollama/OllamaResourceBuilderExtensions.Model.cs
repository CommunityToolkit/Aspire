using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace Aspire.Hosting;

public static partial class OllamaResourceBuilderExtensions
{
    /// <summary>
    /// Adds a model to the Ollama container.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="modelName">The name of the LLM to download on initial startup.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaModelResource> AddModel(this IResourceBuilder<OllamaResource> builder, string modelName)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName, nameof(modelName));

        string sanitizedModelName = modelName.Split(':')[0].Split('/').Last().Replace('.', '-');
        string resourceName = $"{builder.Resource.Name}-{sanitizedModelName}";

        return AddModel(builder, resourceName, modelName);
    }

    /// <summary>
    /// Adds a model to the Ollama container.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="modelName">The name of the LLM to download on initial startup.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaModelResource> AddModel(this IResourceBuilder<OllamaResource> builder, [ResourceName] string name, string modelName)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName, nameof(modelName));

        builder.ApplicationBuilder.Services.TryAddLifecycleHook<OllamaModelResourceLifecycleHook>();

        builder.Resource.AddModel(modelName);
        var modelResource = new OllamaModelResource(name, modelName, builder.Resource);

        modelResource.AddModelResourceCommand(
            name: "Redownload",
            displayName: "Redownload Model",
            executeCommand: async (modelResource, ollamaClient, logger, notificationService, ct) =>
            {
                await OllamaUtilities.PullModelAsync(modelResource, ollamaClient, modelResource.ModelName, logger, notificationService, ct);
                await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success) });

                return CommandResults.Success();
            },
            displayDescription: $"Redownload the model {modelName}.",
            iconName: "ArrowDownload"
        ).AddModelResourceCommand(
            name: "Delete",
            displayName: "Delete Model",
            executeCommand: async (modelResource, ollamaClient, logger, notificationService, ct) =>
            {
                await ollamaClient.DeleteModelAsync(modelResource.ModelName, ct);
                await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot("Stopped", KnownResourceStateStyles.Success) });

                return CommandResults.Success();
            },
            displayDescription: $"Delete the model {modelName}.",
            iconName: "Delete",
            confirmationMessage: $"Are you sure you want to delete the model {modelName}?"
        ).AddModelResourceCommand(
            name: "ModelInfo",
            displayName: "Print Model Info",
            executeCommand: async (modelResource, ollamaClient, logger, notificationService, ct) =>
            {
                var modelInfo = await ollamaClient.ShowModelAsync(modelResource.ModelName, ct);
                logger.LogInformation("Model Info: {ModelInfo}", modelInfo.ToJson());

                return CommandResults.Success();
            },
            displayDescription: $"Print the info for the model {modelName}.",
            iconName: "Info"
        ).AddModelResourceCommand(
            name: "Stop",
            displayName: "Stop Model",
            executeCommand: async (modelResource, ollamaClient, logger, notificationService, ct) =>
            {
                await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Stopping, KnownResourceStateStyles.Success) });
                await foreach (var result in ollamaClient.GenerateAsync(new OllamaSharp.Models.GenerateRequest { Model = modelResource.ModelName, KeepAlive = "0" }, ct))
                {
                    logger.LogInformation("{Result}", result?.ToJson());
                }
                await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot("Stopped", KnownResourceStateStyles.Success) });

                return CommandResults.Success();
            },
            displayDescription: $"Stop the model {modelName}.",
            iconName: "Stop",
            isHighlighted: true
        );

        var healthCheckKey = $"{name}-{modelName}-health";

        builder.ApplicationBuilder.Services.AddHealthChecks()
            .AddTypeActivatedCheck<OllamaModelHealthCheck>(healthCheckKey, modelResource);

        return builder.ApplicationBuilder
            .AddResource(modelResource)
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a model from Hugging Face to the Ollama container. Only models in GGUF format are supported.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="modelName">The name of the LLM from Hugging Face in GGUF format to download on initial startup.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OllamaModelResource> AddHuggingFaceModel(this IResourceBuilder<OllamaResource> builder, [ResourceName] string name, string modelName)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName, nameof(modelName));

        builder.ApplicationBuilder.Services.TryAddLifecycleHook<OllamaModelResourceLifecycleHook>();

        if (!modelName.StartsWith("hf.co/") && !modelName.StartsWith("huggingface.co/"))
        {
            modelName = "hf.co/" + modelName;
        }

        return AddModel(builder, name, modelName);
    }

    private static OllamaModelResource AddModelResourceCommand(
        this OllamaModelResource modelResource,
        string name,
        string displayName,
        Func<OllamaModelResource, IOllamaApiClient, ILogger, ResourceNotificationService, CancellationToken, Task<ExecuteCommandResult>> executeCommand,
        string? displayDescription,
        object? parameter = null,
        string? confirmationMessage = null,
        string? iconName = null,
        IconVariant? iconVariant = IconVariant.Filled,
        bool isHighlighted = false)
    {
        modelResource.Annotations.Add(new ResourceCommandAnnotation(
            name: name,
            displayName: displayName,
            updateState: context =>
                context.ResourceSnapshot.State?.Text == KnownResourceStates.Running ?
                    ResourceCommandState.Enabled :
                    ResourceCommandState.Disabled,
            executeCommand: async context =>
            {
                (var success, var endpoint) = await OllamaUtilities.TryGetEndpointAsync(modelResource, context.CancellationToken);

                if (!success || endpoint is null)
                {
                    return new ExecuteCommandResult { Success = false, ErrorMessage = "Invalid connection string" };
                }

                var ollamaClient = new OllamaApiClient(endpoint);
                var logger = context.ServiceProvider.GetRequiredService<ResourceLoggerService>().GetLogger(modelResource);
                var notificationService = context.ServiceProvider.GetRequiredService<ResourceNotificationService>();

                return await executeCommand(modelResource, ollamaClient, logger, notificationService, context.CancellationToken);
            },
            displayDescription: displayDescription,
            parameter: parameter,
            confirmationMessage: confirmationMessage,
            iconName: iconName,
            iconVariant: iconVariant,
            isHighlighted: isHighlighted
        ));

        return modelResource;
    }

}
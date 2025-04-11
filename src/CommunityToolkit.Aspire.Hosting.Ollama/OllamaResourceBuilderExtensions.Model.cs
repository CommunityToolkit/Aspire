using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Ollama;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using System.Data.Common;
using System.Globalization;

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

        builder.Resource.AddModel(modelName);
        var modelResource = new OllamaModelResource(name, modelName, builder.Resource);

        var healthCheckKey = $"{name}-{modelName}-health";

        builder.ApplicationBuilder.Services.AddHealthChecks()
            .AddTypeActivatedCheck<OllamaModelHealthCheck>(healthCheckKey, modelResource);

        return builder.ApplicationBuilder
            .AddResource(modelResource)
            .WithHealthCheck(healthCheckKey)
            .WithModelCommands(modelName)
            .WithModelDownload();
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

        if (!modelName.StartsWith("hf.co/") && !modelName.StartsWith("huggingface.co/"))
        {
            modelName = "hf.co/" + modelName;
        }

        return AddModel(builder, name, modelName);
    }

    private static IResourceBuilder<OllamaModelResource> WithModelCommands(this IResourceBuilder<OllamaModelResource> builder, string modelName) =>
        builder.AddModelResourceCommand(
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

    private static IResourceBuilder<OllamaModelResource> AddModelResourceCommand(
        this IResourceBuilder<OllamaModelResource> builder,
        string name,
        string displayName,
        Func<OllamaModelResource, IOllamaApiClient, ILogger, ResourceNotificationService, CancellationToken, Task<ExecuteCommandResult>> executeCommand,
        string? displayDescription,
        object? parameter = null,
        string? confirmationMessage = null,
        string? iconName = null,
        IconVariant? iconVariant = IconVariant.Filled,
        bool isHighlighted = false) =>
            builder.WithCommand(
                name: name,
                displayName: displayName,
                executeCommand: async context =>
                {
                    var modelResource = builder.Resource;
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
                commandOptions: new()
                {
                    Description = displayDescription,
                    Parameter = parameter,
                    ConfirmationMessage = confirmationMessage,
                    IconName = iconName,
                    IconVariant = iconVariant,
                    IsHighlighted = isHighlighted,
                    UpdateState = context =>
                    context.ResourceSnapshot.State?.Text == KnownResourceStates.Running ?
                        ResourceCommandState.Enabled :
                        ResourceCommandState.Disabled,
                });

    private static IResourceBuilder<OllamaModelResource> WithModelDownload(this IResourceBuilder<OllamaModelResource> builder)
    {
        builder.ApplicationBuilder.Eventing.Subscribe<ResourceReadyEvent>(builder.Resource.Parent, (@event, cancellationToken) =>
        {
            var loggerService = @event.Services.GetRequiredService<ResourceLoggerService>();
            var notificationService = @event.Services.GetRequiredService<ResourceNotificationService>();

            if (builder.Resource is not OllamaModelResource modelResource)
            {
                return Task.CompletedTask;
            }

            var logger = loggerService.GetLogger(modelResource);
            string model = modelResource.ModelName;

            _ = Task.Run(async () =>
            {
                try
                {
                    var connectionString = await modelResource.ConnectionStringExpression.GetValueAsync(cancellationToken).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot("No connection string", KnownResourceStateStyles.Error) });
                        return;
                    }

                    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out _))
                    {
                        var connectionBuilder = new DbConnectionStringBuilder
                        {
                            ConnectionString = connectionString
                        };

                        if (connectionBuilder.ContainsKey("Endpoint") && Uri.TryCreate(connectionBuilder["Endpoint"].ToString(), UriKind.Absolute, out var endpoint))
                        {
                            connectionString = endpoint.ToString();
                        }
                    }

                    var ollamaClient = new OllamaApiClient(new Uri(connectionString));

                    await notificationService.PublishUpdateAsync(modelResource, state => state with
                    {
                        State = new ResourceStateSnapshot($"Checking {model}", KnownResourceStateStyles.Info),
                        Properties = [.. state.Properties, new(CustomResourceKnownProperties.Source, model)]
                    });

                    var hasModel = await HasModelAsync(ollamaClient, model, cancellationToken);

                    if (!hasModel)
                    {
                        logger.LogInformation("{TimeStamp}: [{Model}] needs to be downloaded for {ResourceName}",
                            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                            model,
                            modelResource.Name);
                        await OllamaUtilities.PullModelAsync(modelResource, ollamaClient, model, logger, notificationService, cancellationToken);
                    }
                    else
                    {
                        logger.LogInformation("{TimeStamp}: [{Model}] already exists for {ResourceName}",
                            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                            model,
                            modelResource.Name);
                    }

                    await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success) });
                }
                catch (Exception ex)
                {
                    await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot(ex.Message, KnownResourceStateStyles.Error) });
                }
            }, cancellationToken);

            return Task.CompletedTask;

            static async Task<bool> HasModelAsync(OllamaApiClient ollamaClient, string model, CancellationToken cancellationToken)
            {
                int retryCount = 0;
                while (retryCount < 5)
                {
                    try
                    {
                        var localModels = await ollamaClient.ListLocalModelsAsync(cancellationToken);
                        return localModels.Any(m => m.Name.Equals(model, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (TaskCanceledException)
                    {
                        // wait 30 seconds before retrying
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                        retryCount++;
                    }
                }

                throw new TimeoutException("Failed to list local models after 5 retries. Likely that the container image was not pulled in time, or the container is not running.");
            }
        });

        return builder;
    }
}
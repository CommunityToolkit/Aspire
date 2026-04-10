using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Aspire.Hosting.LlamaCpp;

namespace Aspire.Hosting.ApplicationModel;

internal static class LlamaCppIResourceBuilderExtensions
{
    private const string ReasoningArgument = "LLAMA_ARG_REASONING";
    private const string ContextSizeArgument = "LLAMA_ARG_CTX_SIZE";
    private const string MultimodalProjectionUrlArgument = "LLAMA_ARG_MMPROJ_URL";
    private const string MultimodalProjectionFileArgument = "LLAMA_ARG_MMPROJ";
    private const string ModelArgument = "LLAMA_ARG_MODEL";
    private const string ModelAliasArgument = "LLAMA_ARG_ALIAS";
    private const string ModelUrlArgument = "LLAMA_ARG_MODEL_URL";
    private const string ApiKeyArgument = "LLAMA_API_KEY";


    public static IResourceBuilder<LlamaCppServerResource> AddLlamaServer(this IDistributedApplicationBuilder builder,
            [ResourceName] string name, string modelUrl, int? port = null,
            LlamaCppServerPlatformOptimization optimization = LlamaCppServerPlatformOptimization.Default)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(modelUrl, nameof(modelUrl));

        var resource = new LlamaCppServerResource(name);

        resource.ModelName = Path.GetFileName(modelUrl);
        return builder.AddResource(resource)
              .WithAnnotation(new ContainerImageAnnotation
              {
                  Image = LlamaCppServerContainerImageTags.Image,
                  Tag = LlamaCppServerContainerImageTags.GetTag(optimization),
                  Registry = LlamaCppServerContainerImageTags.Registry
              })
              .WithEnvironment(ModelUrlArgument, modelUrl)
              .WithHttpEndpoint(port, targetPort: 8080, LlamaCppServerResource.LlamaServerEndpointName)
              .WithHttpHealthCheck("/");
    }
    public static IResourceBuilder<LlamaCppServerResource> WithEnvironment(this IResourceBuilder<LlamaCppServerResource> builder, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        builder.Resource.EnvironmentArgs.AddRange(name);
        ((IResourceBuilder<ContainerResource>)builder).WithEnvironment(name, value);
        return builder;
    }
    public static IResourceBuilder<LlamaCppServerResource> WithReasoning(this IResourceBuilder<LlamaCppServerResource> builder, bool useReasoning = true)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        if (builder.Resource.EnvironmentArgs.Contains(ReasoningArgument))
        {
            throw new InvalidOperationException("Reasoning was already defined and cannot be set again. Make sure that WithReasoning is called at most once.");
        }
        return builder.WithEnvironment(ReasoningArgument, useReasoning ? "on" : "off");
    }
    public static IResourceBuilder<LlamaCppServerResource> WithApikeys(this IResourceBuilder<LlamaCppServerResource> builder, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(keys, nameof(keys));

        if (builder.Resource.EnvironmentArgs.Contains(ApiKeyArgument))
        {
            throw new InvalidOperationException("The instance already defined Api keys. Make sure that WithApikeys is called at most once.");
        }
        var values = string.Join(",", keys);
        return builder.WithEnvironment(ApiKeyArgument, values);
    }
    public static IResourceBuilder<LlamaCppServerResource> WithContextSize(this IResourceBuilder<LlamaCppServerResource> builder, int size = 0)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        if (builder.Resource.EnvironmentArgs.Contains(ContextSizeArgument))
        {
            throw new InvalidOperationException("Context size was already defined and cannot be set again. Make sure that WithContextSize is called at most once.");
        }
        return builder.WithEnvironment(ContextSizeArgument, size.ToString());
    }
    public static IResourceBuilder<LlamaCppServerResource> WithModelAlias(this IResourceBuilder<LlamaCppServerResource> builder, string alias)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
       
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new InvalidOperationException("Alias cannot be empty. Make sure that WithModelAlias is called with a valid alias.");
        }
        if (builder.Resource.EnvironmentArgs.Contains(ModelAliasArgument))
        {
            throw new InvalidOperationException("Model alias was already defined and cannot be set again. Make sure that WithModelAlias is called at most once.");
        }
        return builder.WithEnvironment(ModelAliasArgument, alias);
    }
    public static IResourceBuilder<LlamaCppServerResource> WithMultimodalProjection(this IResourceBuilder<LlamaCppServerResource> builder, string projectionFileUrl)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(projectionFileUrl, nameof(projectionFileUrl));

        if (builder.Resource.EnvironmentArgs.Contains(MultimodalProjectionFileArgument))
        {
            throw new InvalidOperationException("Projection file url was already defined and cannot be set again. Make sure that WithMultimodalProjection is called at most once.");
        }
        return builder.WithEnvironment(MultimodalProjectionUrlArgument, projectionFileUrl)
                      .WithEnvironment(MultimodalProjectionFileArgument,
                              $"/models/mmproj/{Path.GetFileName(projectionFileUrl)}");
    }

    public static IResourceBuilder<LlamaCppServerResource> WithDataVolume(this IResourceBuilder<LlamaCppServerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        if (!string.IsNullOrWhiteSpace(builder.Resource.VolumeName))
        {
            throw new InvalidOperationException("The LlamaServer resource already has a data volume associated. It can not have more than one.");
        }
        var volumeName = name ?? VolumeNameGenerator.Generate(builder, builder.Resource.Name);
        builder.Resource.VolumeName = volumeName;
        return builder.WithVolume(volumeName, "/models", isReadOnly)
            .WithEnvironment(ModelArgument, $"/models/{builder.Resource.ModelName}");
    }
    public static IResourceBuilder<LlamaCppServerResource> WithDataVolume(this IResourceBuilder<LlamaCppServerResource> builder, IResourceBuilder<LlamaCppServerResource> volumeOwner, bool isReadOnly = false)
    {
        if (builder == volumeOwner)
        {
            throw new InvalidOperationException($"The {nameof(volumeOwner)} object must be different than the current builder.");
        }
        var name = volumeOwner.Resource.VolumeName;

        if (volumeOwner.Resource.ModelName == builder.Resource.ModelName)
        {
            builder.WaitFor(volumeOwner);
        }

        return builder.WithDataVolume(name, isReadOnly);
    }
}

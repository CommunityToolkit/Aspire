// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Azure Storage Explorer resources to the application model.
/// </summary>
public static class AzureStorageExplorerBuilderExtensions
{
    private const int AzureStorageExplorerPort = 8080;
    
    /// <summary>
    /// Adds an Azure Storage Explorer resource to the application model.
    /// The default image is <inheritdoc cref="AzureStorageExplorerContainerImageTags.Image"/> and the tag is <inheritdoc cref="AzureStorageExplorerContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="port">The host port for the Azure Storage Explorer instance.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an Azure Storage Explorer container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var storage = builder.AddAzureStorage("storage")
    ///     .RunAsEmulator(azurite =>
    ///     {
    ///         azurite
    ///             .WithBlobPort(27000)
    ///             .WithQueuePort(27001)
    ///             .WithTablePort(27002);
    ///     });
    /// var blobs = storage.AddBlobs("blobs");
    /// 
    /// var azureStorageExplorer = builder
    ///     .AddAzureStorageExplorer("explorer")
    ///     .WithAzurite()
    ///     .WithBlobs(blobs);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    internal static IResourceBuilder<AzureStorageExplorerResource> AddAzureStorageExplorer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var explorer = new AzureStorageExplorerResource(name);

        return builder.AddResource(explorer)
            .WithHttpEndpoint(
                port: port,
                targetPort: AzureStorageExplorerPort,
                name: AzureStorageExplorerResource.PrimaryEndpointName
            )
            .WithImage(AzureStorageExplorerContainerImageTags.Image, AzureStorageExplorerContainerImageTags.Tag)
            .WithImageRegistry(AzureStorageExplorerContainerImageTags.Registry)
            .ExcludeFromManifest();
    }
    
    /// <summary>
    /// Allows Azure Storage Explorer to work with the Azure Emulator (Azurite).
    /// </summary>
    /// <param name="builder">The builder for the <see cref="AzureStorageExplorerResource"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    internal static IResourceBuilder<AzureStorageExplorerResource> WithAzurite(
        this IResourceBuilder<AzureStorageExplorerResource> builder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment("AZURITE", "true");
    }

    internal static IResourceBuilder<AzureStorageExplorerResource> WithStorageResource(
        this IResourceBuilder<AzureStorageExplorerResource> builder, 
        IResourceBuilder<IResourceWithConnectionString> resourceBuilder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment(e =>
        {
            e.EnvironmentVariables["AZURE_STORAGE_CONNECTIONSTRING"] =
                new ConnectionStringReference(resourceBuilder.Resource, optional: false);
        });
    }
}
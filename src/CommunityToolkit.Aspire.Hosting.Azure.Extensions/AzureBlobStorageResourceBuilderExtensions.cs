// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Azure Storage Explorer resources to the application model.
/// </summary>
public static class AzureBlobStorageResourceBuilderExtensions
{
    /// <summary>
    /// Adds an Azure Storage Explorer instance to a Blob storage resource.
    /// </summary>
    /// <param name="blobs">The builder for the <see cref="AzureBlobStorageResource"/>.</param>
    /// <param name="configureContainer">Configuration callback for Azure Storage Explorer container resource.</param>
    /// <param name="name">The name of the resource.</param>
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
    /// var blobs = storage.AddBlobs("blobs")
    ///     .WithAzureStorageExplorer();
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<AzureBlobStorageResource> WithAzureStorageExplorer(
        this IResourceBuilder<AzureBlobStorageResource> blobs,
        Action<IResourceBuilder<AzureStorageExplorerResource>>? configureContainer = null,
        [ResourceName] string? name = null
    )
    {
        string resourceNname = name ?? $"{blobs.Resource.Name}-explorer";
        var builder = blobs.ApplicationBuilder
            .AddAzureStorageExplorer(resourceNname)
            .WithStorageResource(blobs)
            .WithParentRelationship(blobs);

        if (blobs.Resource.Parent.IsEmulator)
        {
            builder.WithAzurite();
        }

        configureContainer?.Invoke(builder);
        
        return blobs;
    }
}
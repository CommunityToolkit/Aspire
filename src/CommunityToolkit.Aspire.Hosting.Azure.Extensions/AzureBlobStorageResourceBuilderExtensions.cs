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
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AzureBlobStorageResource> WithAzureStorageExplorer(
        this IResourceBuilder<AzureBlobStorageResource> blobs,
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
         
        return blobs;
    }
}
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Azure Storage Explorer resources to the application model.
/// </summary>
public static class AzureQueueStorageResourceBuilderExtensions
{
    /// <summary>
    /// Adds an Azure Storage Explorer instance to a Queue storage resource.
    /// </summary>
    /// <param name="queues">The builder for the <see cref="AzureQueueStorageResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AzureQueueStorageResource> WithAzureStorageExplorer(
        this IResourceBuilder<AzureQueueStorageResource> queues,
        [ResourceName] string? name = null
    )
    {
        string resourceNname = name ?? $"{queues.Resource.Name}-explorer";
        var builder = queues.ApplicationBuilder
            .AddAzureStorageExplorer(resourceNname)
            .WithStorageResource(queues)
            .WithParentRelationship(queues);

        if (queues.Resource.Parent.IsEmulator)
        {
            builder.WithAzurite();
        }
        
        return queues;
    }
}
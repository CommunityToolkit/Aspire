// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Azure Storage Explorer resources to the application model.
/// </summary>
public static class AzureTableStorageResourceBuilderExtensions
{
    /// <summary>
    /// Adds an Azure Storage Explorer instance to a Table storage resource.
    /// </summary>
    /// <param name="tables">The builder for the <see cref="AzureTableStorageResource"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AzureTableStorageResource> WithAzureStorageExplorer(
        this IResourceBuilder<AzureTableStorageResource> tables,
        [ResourceName] string? name = null
    )
    {
        string resourceNname = name ?? $"{tables.Resource.Name}-explorer";
        var builder = tables.ApplicationBuilder
            .AddAzureStorageExplorer(resourceNname)
            .WithStorageResource(tables)
            .WithParentRelationship(tables);

        if (tables.Resource.Parent.IsEmulator)
        {
            builder.WithAzurite();
        }
        
        return tables;
    }
}
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an Azure Storage Explorer container.
/// </summary>
public class AzureStorageExplorerResource : ContainerResource
{
    internal const string PrimaryEndpointName = "http";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureStorageExplorerResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    public AzureStorageExplorerResource(
        [ResourceName] string name
    ) : base(name)
    {
        PrimaryEndpoint = new(this, PrimaryEndpointName);
    }

    /// <summary>
    /// Gets the primary endpoint for the Azure Storage Explorer instance.
    /// </summary>
    public EndpointReference PrimaryEndpoint { get; }

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);
}
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Umami instance resource.
/// </summary>
public class UmamiResource : ContainerResource
{
    internal const string PrimaryEndpointName = "http";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="UmamiResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="secret">A parameter that contains the Umami app secret.</param>
    public UmamiResource([ResourceName] string name, ParameterResource secret) : base(name)
    {
        ArgumentNullException.ThrowIfNull(secret);
        
        PrimaryEndpoint = new(this, PrimaryEndpointName);
        SecretParameter = secret;
    }

    /// <summary>
    /// Gets the primary endpoint for the Umami instance.
    /// </summary>
    public EndpointReference PrimaryEndpoint { get; }

    /// <summary>
    /// Gets the parameter that contains the Umami app secret.
    /// </summary>
    public ParameterResource SecretParameter { get; }
}
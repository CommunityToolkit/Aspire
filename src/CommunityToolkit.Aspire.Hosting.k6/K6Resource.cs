// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Grafana k6 instance
/// </summary>
[AspireExport(ExposeProperties = true)]
public class K6Resource(string name) : ContainerResource(name)
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Grafana k6 instance.
    /// This endpoint is used for all API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental

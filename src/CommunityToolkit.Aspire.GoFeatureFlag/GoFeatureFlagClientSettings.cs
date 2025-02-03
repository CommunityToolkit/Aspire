// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using OpenFeature.Contrib.Providers.GOFeatureFlag;
using System.Data.Common;

namespace CommunityToolkit.Aspire.GoFeatureFlag;

/// <summary>
/// Provides the client configuration settings for connecting to a GO Feature Flag server.
/// </summary>
public sealed class GoFeatureFlagClientSettings
{
    private const string ConnectionStringEndpoint = "Endpoint";

    /// <summary>
    /// The endpoint URI string of the GO Feature Flag server to connect to.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the GO Feature Flag health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a integer value that indicates the GO Feature Flag health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

    /// <summary>
    /// Gets or sets the provider options that will be used to configure the GO Feature Flag client.
    /// </summary>
    public GoFeatureFlagProviderOptions ProviderOptions { get; set; } = new();

    internal void ParseConnectionString(string? connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            Endpoint = uri;
        }
        else
        {
            var connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.TryGetValue(ConnectionStringEndpoint, out var endpoint) && Uri.TryCreate(endpoint.ToString(), UriKind.Absolute, out var serviceUri))
            {
                Endpoint = serviceUri;
            }
        }
    }
}

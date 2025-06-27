// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using SurrealDb.Net;

namespace CommunityToolkit.Aspire.SurrealDb;

/// <summary>
/// Provides the client configuration settings for connecting to a SurrealDB server using <see cref="SurrealDbClient"/>.
/// </summary>
public sealed class SurrealDbClientSettings
{
    /// <summary>
    /// The defined <see cref="SurrealDbClient"/> options used to connect to the SurrealDB server.
    /// </summary>
    public SurrealDbOptions? Options { get; set; }
    
    /// <summary>
    /// Gets or sets the Service lifetime to register services under.
    /// </summary>
    /// <value>
    /// The default value is <see langword="ServiceLifetime.Singleton"/>.
    /// </value>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;
    
    /// <summary>
    /// Gets or sets a boolean value that indicates whether the SurrealDB health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a integer value that indicates the SurrealDB health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }
}
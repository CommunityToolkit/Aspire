// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using KurrentDB.Client;

namespace CommunityToolkit.Aspire.KurrentDB;

/// <summary>
/// Provides the client configuration settings for connecting to a KurrentDB server using <see cref="KurrentDBClient"/>.
/// </summary>
public sealed class KurrentDBSettings
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the KurrentDB health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }
    
    /// <summary>
    /// Gets or sets the timeout duration for the health check.
    /// </summary>
    /// <value>
    /// The default value is <see langword="null"/>.
    /// </value>
    public TimeSpan? HealthCheckTimeout { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableTracing { get; set; }
}

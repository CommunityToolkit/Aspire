// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Sftp;

/// <summary>
/// Provides the client configuration settings for connecting to an SFTP server using SSH.NET.
/// </summary>
public sealed class SftpSettings
{
    /// <summary>
    /// Gets or sets the connection string in the format "sftp://host:port".
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the username for SFTP authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for SFTP authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the path to a private key file for SFTP authentication.
    /// </summary>
    public string? PrivateKeyFile { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the SFTP health check is disabled or not.
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

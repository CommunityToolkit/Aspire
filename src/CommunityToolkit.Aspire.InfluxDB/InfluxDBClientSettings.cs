// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Data.Common;

namespace CommunityToolkit.Aspire.InfluxDB;

/// <summary>
/// Provides the client configuration settings for connecting to an InfluxDB server using InfluxDBClient.
/// </summary>
public sealed class InfluxDBClientSettings
{
    private const string ConnectionStringUrl = "Url";
    private const string ConnectionStringToken = "Token";
    private const string ConnectionStringOrganization = "Organization";
    private const string ConnectionStringBucket = "Bucket";

    /// <summary>
    /// The URL of the InfluxDB server to connect to.
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>
    /// The authentication token for the InfluxDB server.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// The organization name for the InfluxDB server.
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// The default bucket name for the InfluxDB server.
    /// </summary>
    public string? Bucket { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the InfluxDB health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a integer value that indicates the InfluxDB health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

    internal void ParseConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            Url = uri;
        }
        else
        {
            var connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.TryGetValue(ConnectionStringUrl, out var url) && Uri.TryCreate(url.ToString(), UriKind.Absolute, out var serviceUri))
            {
                Url = serviceUri;
            }

            if (connectionBuilder.TryGetValue(ConnectionStringToken, out var token))
            {
                Token = token.ToString();
            }

            if (connectionBuilder.TryGetValue(ConnectionStringOrganization, out var organization))
            {
                Organization = organization.ToString();
            }

            if (connectionBuilder.TryGetValue(ConnectionStringBucket, out var bucket))
            {
                Bucket = bucket.ToString();
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Renci.SshNet;

namespace CommunityToolkit.Aspire.Sftp;

/// <summary>
/// Checks whether a connection can be made to an SFTP server using the supplied connection settings.
/// </summary>
public class SftpHealthCheck : IHealthCheck, IDisposable
{
    private readonly SftpClient _client;

    /// <inheritdoc/>
    public SftpHealthCheck(SftpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.ConnectionString);

        var (host, port) = ParseConnectionString(settings.ConnectionString);

        if (string.IsNullOrEmpty(settings.Username))
        {
            throw new InvalidOperationException("Username must be provided for SFTP health check.");
        }

        ConnectionInfo connectionInfo;

        if (!string.IsNullOrEmpty(settings.PrivateKeyFile))
        {
            var privateKeyFile = new PrivateKeyFile(settings.PrivateKeyFile);
            connectionInfo = new ConnectionInfo(host, port, settings.Username, new PrivateKeyAuthenticationMethod(settings.Username, privateKeyFile));
        }
        else if (!string.IsNullOrEmpty(settings.Password))
        {
            connectionInfo = new ConnectionInfo(host, port, settings.Username, new PasswordAuthenticationMethod(settings.Username, settings.Password));
        }
        else
        {
            throw new InvalidOperationException("Either Password or PrivateKeyFile must be provided for SFTP health check.");
        }

        _client = new SftpClient(connectionInfo);
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_client.IsConnected)
            {
                _client.Connect();
            }

            var files = _client.ListDirectory("/");

            if (files.Any())
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        if (_client.IsConnected)
        {
            _client.Disconnect();
        }
        _client.Dispose();
    }

    private static (string Host, int Port) ParseConnectionString(string connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 22;
            return (host, port);
        }

        throw new InvalidOperationException($"The connection string '{connectionString}' is not in the correct format. Expected format: 'sftp://host:port'");
    }
}

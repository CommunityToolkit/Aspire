// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Renci.SshNet;

namespace CommunityToolkit.Aspire.Sftp.Tests;

public class AspireSftpClientExtensionsTest
{
    private const string DefaultConnectionName = "sftp";
    private const string DefaultConnectionString = "sftp://localhost:22";

    [Fact]
    public void AddSftpClient_RegistersSingleton()
    {
        var builder = CreateBuilder(DefaultConnectionString);

        builder.AddSftpClient(DefaultConnectionName, settings =>
        {
            settings.Username = "testuser";
            settings.Password = "testpassword";
            settings.DisableHealthChecks = true;
            settings.DisableTracing = true;
        });

        using var host = builder.Build();

        var client = host.Services.GetRequiredService<SftpClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void AddKeyedSftpClient_RegistersKeyedSingleton()
    {
        var builder = CreateBuilder(DefaultConnectionString);

        builder.AddKeyedSftpClient(DefaultConnectionName, settings =>
        {
            settings.Username = "testuser";
            settings.Password = "testpassword";
            settings.DisableHealthChecks = true;
            settings.DisableTracing = true;
        });

        using var host = builder.Build();

        var client = host.Services.GetRequiredKeyedService<SftpClient>(DefaultConnectionName);
        Assert.NotNull(client);
    }

    [Fact]
    public void CanAddMultipleKeyedServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:sftp1", "sftp://localhost:22"),
            new KeyValuePair<string, string?>("ConnectionStrings:sftp2", "sftp://localhost:2222"),
            new KeyValuePair<string, string?>("ConnectionStrings:sftp3", "sftp://localhost:2223"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:Username", "user1"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:Password", "pass1"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:DisableHealthChecks", "true"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:DisableTracing", "true"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp2:Username", "user2"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp2:Password", "pass2"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp2:DisableHealthChecks", "true"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp2:DisableTracing", "true"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp3:Username", "user3"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp3:Password", "pass3"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp3:DisableHealthChecks", "true"),
            new KeyValuePair<string, string?>("Aspire:Sftp:Client:sftp3:DisableTracing", "true"),
        ]);

        builder.AddSftpClient("sftp1");
        builder.AddKeyedSftpClient("sftp2");
        builder.AddKeyedSftpClient("sftp3");

        using var host = builder.Build();

        var client1 = host.Services.GetRequiredService<SftpClient>();
        var client2 = host.Services.GetRequiredKeyedService<SftpClient>("sftp2");
        var client3 = host.Services.GetRequiredKeyedService<SftpClient>("sftp3");

        Assert.NotSame(client1, client2);
        Assert.NotSame(client1, client3);
        Assert.NotSame(client2, client3);
    }

    [Fact]
    public void AddSftpClient_ThrowsWhenMissingUsername()
    {
        var builder = CreateBuilder(DefaultConnectionString);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.AddSftpClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = true;
                settings.DisableTracing = true;
            });
        });

        Assert.Contains("Username", ex.Message);
    }

    [Fact]
    public void AddSftpClient_ThrowsWhenMissingConnectionString()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.AddSftpClient(DefaultConnectionName, settings =>
            {
                settings.DisableHealthChecks = true;
                settings.DisableTracing = true;
                settings.Username = "testuser";
            });
        });

        Assert.Contains("ConnectionString", ex.Message);
    }

    [Fact]
    public void AddSftpClient_ThrowsWhenMissingPasswordAndPrivateKey()
    {
        var builder = CreateBuilder(DefaultConnectionString);

        builder.AddSftpClient(DefaultConnectionName, settings =>
        {
            settings.Username = "testuser";
            settings.DisableHealthChecks = true;
            settings.DisableTracing = true;
        });

        using var host = builder.Build();

        Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<SftpClient>());
    }

    [Fact]
    public void AddSftpClient_HealthCheckRegisteredByDefault()
    {
        var builder = CreateBuilder(DefaultConnectionString);

        builder.AddSftpClient(DefaultConnectionName, settings =>
        {
            settings.Username = "testuser";
            settings.Password = "testpassword";
        });

        using var host = builder.Build();

        var healthCheckService = host.Services.GetRequiredService<HealthCheckService>();
        Assert.NotNull(healthCheckService);
    }

    private static HostApplicationBuilder CreateBuilder(string connectionString)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>($"ConnectionStrings:{DefaultConnectionName}", connectionString)
        ]);
        return builder;
    }
}

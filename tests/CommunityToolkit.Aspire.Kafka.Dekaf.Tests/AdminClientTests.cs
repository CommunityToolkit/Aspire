// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Dekaf.Admin;
using Dekaf.Diagnostics;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class AdminClientTests
{
    [Theory]
    [InlineData(false, "Named")]
    [InlineData(true, "Named")]
    [InlineData(false, "ConnectionString")]
    [InlineData(true, "ConnectionString")]
    [InlineData(false, "Settings")]
    [InlineData(true, "Settings")]
    public async Task BootstrapServerOverridesReplaceDefaultControllers(bool keyed, string source)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = source == "ConnectionString" ? "override:9092" : null;
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:BootstrapControllers:0"] = "default-controller:9093";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:ClientId"] = "admin-client";
        if (source == "Named")
        {
            builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:messaging:Config:BootstrapServers:0"] = "override:9092";
        }
        void Configure(KafkaAdminClientSettings settings)
        {
            if (source == "Settings")
            {
                settings.ConnectionString = "override:9092";
            }
        }
        if (keyed)
        {
            builder.AddKeyedDekafKafkaAdminClient("messaging", Configure);
        }
        else
        {
            builder.AddDekafKafkaAdminClient("messaging", Configure);
        }

        await using var services = builder.Services.BuildServiceProvider();
        var client = keyed ? services.GetRequiredKeyedService<IAdminClient>("messaging") : services.GetRequiredService<IAdminClient>();
        var options = ClientTestHelpers.GetOptions<AdminClientOptions>(client);
        Assert.Equal(["override:9092"], options.BootstrapServers);
        Assert.Empty(options.BootstrapControllers);
        Assert.Equal("admin-client", options.ClientId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamedControllersReplaceDefaultBootstrapServers(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:ConnectionString"] = "default-connection:9092";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:BootstrapServers:0"] = "default:9092";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:messaging:Config:BootstrapControllers:0"] = "named-controller:9093";
        if (keyed)
        {
            builder.AddKeyedDekafKafkaAdminClient("messaging");
        }
        else
        {
            builder.AddDekafKafkaAdminClient("messaging");
        }

        await using var services = builder.Services.BuildServiceProvider();
        var client = keyed ? services.GetRequiredKeyedService<IAdminClient>("messaging") : services.GetRequiredService<IAdminClient>();
        var options = ClientTestHelpers.GetOptions<AdminClientOptions>(client);
        Assert.Empty(options.BootstrapServers);
        Assert.Equal(["named-controller:9093"], options.BootstrapControllers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeBootstrapServersAcceptCommaSeparatedValues(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:BootstrapServers"] = "first:9092, second:9092";
        if (keyed)
        {
            builder.AddKeyedDekafKafkaAdminClient("messaging");
        }
        else
        {
            builder.AddDekafKafkaAdminClient("messaging");
        }
        await using var services = builder.Services.BuildServiceProvider();
        var client = keyed ? services.GetRequiredKeyedService<IAdminClient>("messaging") : services.GetRequiredService<IAdminClient>();
        Assert.Equal(["first:9092", "second:9092"], ClientTestHelpers.GetOptions<AdminClientOptions>(client).BootstrapServers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeConfigurationPreservesSecurityAndReplacesBootstrapServers(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:BootstrapServers:0"] = "default:9092";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:BootstrapServers:1"] = "backup:9092";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:ClientId"] = "default-admin";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:messaging:Config:ClientId"] = "named-admin";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:RequestTimeoutMs"] = "2468";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:UseTls"] = "true";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:SaslMechanism"] = "ScramSha512";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:SaslUsername"] = "test-user";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:SaslPassword"] = "test-password";
        if (keyed)
        {
            builder.AddKeyedDekafKafkaAdminClient("messaging");
        }
        else
        {
            builder.AddDekafKafkaAdminClient("messaging");
        }
        await using var services = builder.Services.BuildServiceProvider();
        var client = keyed ? services.GetRequiredKeyedService<IAdminClient>("messaging") : services.GetRequiredService<IAdminClient>();
        var options = ClientTestHelpers.GetOptions<AdminClientOptions>(client);
        Assert.Equal(["localhost:19092"], options.BootstrapServers);
        Assert.Equal("named-admin", options.ClientId);
        Assert.Equal(2468, options.RequestTimeoutMs);
        Assert.True(options.UseTls);
        Assert.Equal(SaslMechanism.ScramSha512, options.SaslMechanism);
        Assert.Equal("test-user", options.SaslUsername);
        Assert.Equal("test-password", options.SaslPassword);
    }

    [Fact]
    public async Task AdminClientCanUseNativeBootstrapServersWithoutConnectionString()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:Config:BootstrapServers:0"] = "native:9092";
        builder.AddDekafKafkaAdminClient("messaging");
        await using var services = builder.Services.BuildServiceProvider();
        Assert.Equal(["native:9092"], ClientTestHelpers.GetOptions<AdminClientOptions>(services.GetRequiredService<IAdminClient>()).BootstrapServers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConnectionAndBuilderOverridesAreApplied(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Services.AddSingleton("custom-admin");
        if (keyed)
        {
            builder.AddKeyedDekafKafkaAdminClient("messaging",
                settings => settings.ConnectionString = "settings:9092",
                (services, options) => new AdminClientBuilder().WithBootstrapServers(options.BootstrapServers.ToArray()).WithClientId(services.GetRequiredService<string>()).Build());
        }
        else
        {
            builder.AddDekafKafkaAdminClient("messaging",
                settings => settings.ConnectionString = "settings:9092",
                (services, options) => new AdminClientBuilder().WithBootstrapServers(options.BootstrapServers.ToArray()).WithClientId(services.GetRequiredService<string>()).Build());
        }

        await using var services = builder.Services.BuildServiceProvider();
        var client = keyed ? services.GetRequiredKeyedService<IAdminClient>("messaging") : services.GetRequiredService<IAdminClient>();
        var options = ClientTestHelpers.GetOptions<AdminClientOptions>(client);
        Assert.Equal(["settings:9092"], options.BootstrapServers);
        Assert.Equal("custom-admin", options.ClientId);
        var check = Assert.Single(services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal(keyed ? "Kafka.Dekaf_admin_messaging" : "Kafka.Dekaf_admin", check.Name);
    }

    [Fact]
    public async Task KeyedAdminClientsAreIsolatedAndDisposed()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.AddDekafKafkaAdminClient("messaging");
        builder.AddKeyedDekafKafkaAdminClient("other", settings => settings.ConnectionString = "other:9092");
        var services = builder.Services.BuildServiceProvider();
        var first = services.GetRequiredService<IAdminClient>();
        var second = services.GetRequiredKeyedService<IAdminClient>("other");
        Assert.NotSame(first, second);
        Assert.Same(first, services.GetRequiredService<IAdminClient>());
        Assert.Equal(["other:9092"], ClientTestHelpers.GetOptions<AdminClientOptions>(second).BootstrapServers);
        await services.DisposeAsync();
        Assert.True(((IKafkaClientStatusProvider)first).GetStatus().IsStopped);
        Assert.True(((IKafkaClientStatusProvider)second).GetStatus().IsStopped);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrokerHealthCheckTimesOutAgainstUnresponsiveServer(bool keyed)
    {
        // Keep the port reserved without speaking Kafka. This exercises a real protocol request
        // and avoids a race with another process claiming a port between allocation and use.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = $"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        builder.Configuration["Aspire:Kafka:Dekaf:AdminClient:HealthCheck:Timeout"] = "00:00:00.200";
        if (keyed)
        {
            builder.AddKeyedDekafKafkaAdminClient("messaging");
        }
        else
        {
            builder.AddDekafKafkaAdminClient("messaging");
        }

        await using var services = builder.Services.BuildServiceProvider();
        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Single(report.Entries);
    }

    [Fact]
    public void PublicArgumentsAreValidated()
    {
        Assert.Throws<ArgumentNullException>(() => AspireDekafKafkaAdminClientExtensions.AddDekafKafkaAdminClient(null!, "messaging"));
        Assert.Throws<ArgumentNullException>(() => AspireDekafKafkaAdminClientExtensions.AddKeyedDekafKafkaAdminClient(null!, "messaging"));
        var builder = ClientTestHelpers.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddDekafKafkaAdminClient(null!));
        Assert.Throws<ArgumentException>(() => builder.AddDekafKafkaAdminClient(""));
        Assert.Throws<ArgumentNullException>(() => builder.AddKeyedDekafKafkaAdminClient(null!));
        Assert.Throws<ArgumentException>(() => builder.AddKeyedDekafKafkaAdminClient(""));
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf;
using Dekaf.ShareConsumer;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace CommunityToolkit.Aspire.Kafka.Dekaf.Tests;

public class ShareConsumerConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeBootstrapServersAcceptCommaSeparatedValues(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:BootstrapServers"] = "first:9092, second:9092";
        Register(builder, keyed);
        await using var services = builder.Services.BuildServiceProvider();
        Assert.Equal(["first:9092", "second:9092"], ClientTestHelpers.GetOptions<ShareConsumerOptions>(ClientTestHelpers.GetShareConsumer(services, keyed)).BootstrapServers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShareOptionsAndSecuritySurviveConnectionStringOverride(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:AcknowledgementMode"] = "Explicit";
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:MaxPollRecords"] = "17";
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:FetchMaxWaitMs"] = "123";
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:UseTls"] = "true";
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:SaslMechanism"] = "ScramSha512";
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:SaslUsername"] = "test-user";
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:SaslPassword"] = "test-password";
        Register(builder, keyed, configureBuilder: (_, consumer) => consumer.SubscribeTo("orders", "audit"));
        await using var services = builder.Services.BuildServiceProvider();
        var consumer = ClientTestHelpers.GetShareConsumer(services, keyed);
        var options = ClientTestHelpers.GetOptions<ShareConsumerOptions>(consumer);
        Assert.Equal(["localhost:19092"], options.BootstrapServers);
        Assert.Equal(ShareAcknowledgementMode.Explicit, options.AcknowledgementMode);
        Assert.Equal(17, options.MaxPollRecords);
        Assert.Equal(123, options.FetchMaxWaitMs);
        Assert.True(options.UseTls);
        Assert.Equal(SaslMechanism.ScramSha512, options.SaslMechanism);
        Assert.Equal("test-user", options.SaslUsername);
        Assert.Equal("test-password", options.SaslPassword);
        Assert.Contains("orders", consumer.Subscription);
        Assert.Contains("audit", consumer.Subscription);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingGroupIdFailsOnResolution(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:GroupId"] = null;
        Register(builder, keyed);
        await using var services = builder.Services.BuildServiceProvider();
        Assert.Throws<ArgumentNullException>(() => ClientTestHelpers.GetShareConsumer(services, keyed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfigurationPrecedenceAndNativeOptions(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Kafka:Dekaf:ShareConsumer:ConnectionString"] = "default:9092",
            ["Aspire:Kafka:Dekaf:ShareConsumer:Config:ClientId"] = "default-client",
            ["Aspire:Kafka:Dekaf:ShareConsumer:Config:RequestTimeoutMs"] = "2345",
            ["Aspire:Kafka:Dekaf:ShareConsumer:messaging:ConnectionString"] = "named:9092",
            ["Aspire:Kafka:Dekaf:ShareConsumer:messaging:Config:ClientId"] = "named-client",
            ["Aspire:Kafka:Dekaf:ShareConsumer:messaging:DisableHealthChecks"] = "true"
        });

        Register(builder, keyed);
        await using var services = builder.Services.BuildServiceProvider();
        var client = ClientTestHelpers.GetShareConsumer(services, keyed);
        var options = ClientTestHelpers.GetOptions<ShareConsumerOptions>(client);

        Assert.Equal(["localhost:19092"], options.BootstrapServers);
        Assert.Equal("named-client", options.ClientId);
        Assert.Equal(2345, options.RequestTimeoutMs);
        Assert.Null(services.GetService<HealthCheckService>());
        Assert.Same(client, ClientTestHelpers.GetShareConsumer(services, keyed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettingsAndBuilderOverrideConfiguration(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        var settingsCalls = 0;
        var builderCalls = 0;
        builder.Services.AddSingleton("service-client");
        Register(builder, keyed, settings =>
        {
            settingsCalls++;
            settings.ConnectionString = "settings:9092";
        }, (services, client) =>
        {
            builderCalls++;
            client.WithBootstrapServers("builder:9092").WithClientId(services.GetRequiredService<string>());
        });
        Assert.Equal(1, settingsCalls);
        Assert.Equal(0, builderCalls);

        await using var services = builder.Services.BuildServiceProvider();
        var client = ClientTestHelpers.GetShareConsumer(services, keyed);
        var options = ClientTestHelpers.GetOptions<ShareConsumerOptions>(client);
        Assert.Equal(["builder:9092"], options.BootstrapServers);
        Assert.Equal("service-client", options.ClientId);
        Assert.Same(client, ClientTestHelpers.GetShareConsumer(services, keyed));
        Assert.Equal(1, builderCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanConfigureNativeOptionsWithoutConnectionString(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        Register(builder, keyed, configureBuilder: (_, client) => client
            .WithBootstrapServers("native:9092", "backup:9092")
            .WithClientId("native-client"));
        await using var services = builder.Services.BuildServiceProvider();
        var options = ClientTestHelpers.GetOptions<ShareConsumerOptions>(ClientTestHelpers.GetShareConsumer(services, keyed));
        Assert.Equal(["native:9092", "backup:9092"], options.BootstrapServers);
        Assert.Equal("native-client", options.ClientId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadsBootstrapServersFromConfigArray(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:BootstrapServers:0"] = "first:9092";
        builder.Configuration["Aspire:Kafka:Dekaf:ShareConsumer:Config:BootstrapServers:1"] = "second:9092";
        Register(builder, keyed);
        await using var services = builder.Services.BuildServiceProvider();
        var options = ClientTestHelpers.GetOptions<ShareConsumerOptions>(ClientTestHelpers.GetShareConsumer(services, keyed));
        Assert.Equal(["first:9092", "second:9092"], options.BootstrapServers);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TelemetryAndHealthChecksCanBeDisabled(bool keyed, bool disabled)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        Register(builder, keyed, settings =>
        {
            settings.DisableHealthChecks = disabled;
            settings.DisableMetrics = disabled;
            settings.DisableTracing = disabled;
        });
        await using var services = builder.Services.BuildServiceProvider();
        Assert.Equal(!disabled, services.GetService<HealthCheckService>() is not null);
        Assert.Equal(!disabled, services.GetService<MeterProvider>() is not null);
        Assert.Equal(!disabled, services.GetService<TracerProvider>() is not null);
    }

    [Fact]
    public async Task KeyedClientsAreIsolatedAndDisposedByContainer()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.AddDekafKafkaShareConsumer<string, string>("messaging");
        builder.AddKeyedDekafKafkaShareConsumer<string, string>("messaging");
        builder.AddKeyedDekafKafkaShareConsumer<string, string>("other", settings => settings.ConnectionString = "other:9092");
        var services = builder.Services.BuildServiceProvider();
        var first = services.GetRequiredService<IKafkaShareConsumer<string, string>>();
        var second = services.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("messaging");
        var third = services.GetRequiredKeyedService<IKafkaShareConsumer<string, string>>("other");
        Assert.NotSame(first, second);
        Assert.NotSame(second, third);
        Assert.Equal(["other:9092"], ClientTestHelpers.GetOptions<ShareConsumerOptions>(third).BootstrapServers);
        var checks = services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
        Assert.Equal(["Kafka.Dekaf_shareconsumer<System.String,System.String>", "Kafka.Dekaf_shareconsumer<System.String,System.String>_messaging", "Kafka.Dekaf_shareconsumer<System.String,System.String>_other"], checks.Select(check => check.Name));

        await services.DisposeAsync();
        Assert.True(((global::Dekaf.Diagnostics.IKafkaClientStatusProvider)first).GetStatus().IsStopped);
        Assert.True(((global::Dekaf.Diagnostics.IKafkaClientStatusProvider)second).GetStatus().IsStopped);
        Assert.True(((global::Dekaf.Diagnostics.IKafkaClientStatusProvider)third).GetStatus().IsStopped);
    }

    [Fact]
    public async Task DuplicateRegistrationDoesNotDuplicateHealthChecks()
    {
        var builder = ClientTestHelpers.CreateBuilder();
        Register(builder, keyed: false);
        Register(builder, keyed: false);
        await using var services = builder.Services.BuildServiceProvider();
        Assert.Single(services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingBootstrapServersFailsOnResolution(bool keyed)
    {
        var builder = ClientTestHelpers.CreateBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = null;
        Register(builder, keyed);
        await using var services = builder.Services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => ClientTestHelpers.GetShareConsumer(services, keyed));
    }

    private static void Register(HostApplicationBuilder builder, bool keyed,
        Action<KafkaShareConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ShareConsumerBuilder<string, string>>? configureBuilder = null)
    {
        if (keyed)
        {
            builder.AddKeyedDekafKafkaShareConsumer("messaging", configureSettings, configureBuilder);
        }
        else
        {
            builder.AddDekafKafkaShareConsumer("messaging", configureSettings, configureBuilder);
        }
    }
}

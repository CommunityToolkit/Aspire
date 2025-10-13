// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using InfluxDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CommunityToolkit.Aspire.InfluxDB.Tests;

public class ConformanceTests : ConformanceTests<InfluxDBClient, InfluxDBClientSettings>, IClassFixture<InfluxDBContainerFixture>
{
    private readonly InfluxDBContainerFixture _containerFixture;

    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => true;

    public ConformanceTests(InfluxDBContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
    }

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var connectionString = RequiresDockerAttribute.IsSupported ?
          $"{_containerFixture.GetConnectionString()}" :
          "Url=http://localhost:8086;Token=my-token;Organization=default;Bucket=default";

        configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>(CreateConfigKey("Aspire:InfluxDB:Client", key, "Url"), GetConnectionStringKeyValue(connectionString,"Url")),
                new KeyValuePair<string, string?>(CreateConfigKey("Aspire:InfluxDB:Client", key, "Token"), GetConnectionStringKeyValue(connectionString,"Token")),
                new KeyValuePair<string, string?>(CreateConfigKey("Aspire:InfluxDB:Client", key, "Organization"), GetConnectionStringKeyValue(connectionString,"Organization")),
                new KeyValuePair<string, string?>(CreateConfigKey("Aspire:InfluxDB:Client", key, "Bucket"), GetConnectionStringKeyValue(connectionString,"Bucket")),
                new KeyValuePair<string, string?>($"ConnectionStrings:{key}", $"{connectionString}")
            ]);
    }

    internal static string GetConnectionStringKeyValue(string connectionString, string configKey)
    {
        // from the connection string, extract the key value of the configKey
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var keyValue = part.Split('=');
            if (keyValue.Length == 2 && keyValue[0].Equals(configKey, StringComparison.OrdinalIgnoreCase))
            {
                return keyValue[1];
            }
        }
        return string.Empty;
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<InfluxDBClientSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddInfluxDBClient("influxdb", configureSettings: configure);
        }
        else
        {
            builder.AddKeyedInfluxDBClient(key, configureSettings: configure);
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "InfluxDB": {
                                                       "Client": {
                                                         "Url": "http://localhost:8086",
                                                         "Token": "my-token",
                                                         "Organization": "default",
                                                         "Bucket": "default"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """;

    protected override (string json, string error)[] InvalidJsonToErrorMessage => new[]
        {
            ("""{"Aspire": { "InfluxDB":{ "Client": { "Url": 3 }}}}""", "Value is \"integer\" but should be \"string\""),
            ("""{"Aspire": { "InfluxDB":{ "Client": { "Url": "hello" }}}}""", "Value does not match format \"uri\"")
        };

    protected override void SetHealthCheck(InfluxDBClientSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(InfluxDBClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(InfluxDBClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(InfluxDBClient service)
    {
        using var source = new CancellationTokenSource(100);

        service.HealthAsync(source.Token).Wait();
    }
}

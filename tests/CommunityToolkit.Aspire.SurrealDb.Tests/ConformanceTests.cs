// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SurrealDb.Net;

namespace CommunityToolkit.Aspire.SurrealDb.Tests;

public class ConformanceTests :
    ConformanceTests<SurrealDbClient, SurrealDbClientSettings>,
    IClassFixture<SurrealDbContainerFixture>
{
    private readonly SurrealDbContainerFixture _containerFixture;

    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => true;

    protected override bool CanCreateClientWithoutConnectingToServer => false;

    public ConformanceTests(SurrealDbContainerFixture containerFixture)
    {
        _containerFixture = containerFixture;
    }

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var connectionString = RequiresDockerAttribute.IsSupported ?
            $"{_containerFixture.GetConnectionString()}" :
            "Endpoint=http://localhost:27017";

        configuration.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(CreateConfigKey("Aspire:Surreal:Client", key, "Endpoint"), GetConnectionStringKeyValue(connectionString,"Endpoint")),
            new KeyValuePair<string, string?>($"ConnectionStrings:{key ?? "surreal"}", $"{connectionString}")
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

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<SurrealDbClientSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddSurrealClient("surreal", configureSettings: configure);
        }
        else
        {
            builder.AddKeyedSurrealClient(key, configureSettings: configure);
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "Surreal": {
                                                       "Client": {
                                                         "Endpoint": "http://localhost:19530"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """;

    protected override void SetHealthCheck(SurrealDbClientSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(SurrealDbClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(SurrealDbClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(SurrealDbClient service)
    {
        using var source = new CancellationTokenSource(100);

        Task.Run(() => service.Connect());
    }
}
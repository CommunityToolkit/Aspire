// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using CommunityToolkit.Aspire.Testing;
using KurrentDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.KurrentDB.Tests;

public class ConformanceTests(KurrentDBContainerFixture containerFixture) : ConformanceTests<KurrentDBClient, KurrentDBSettings>, IClassFixture<KurrentDBContainerFixture>
{
    private readonly KurrentDBContainerFixture _containerFixture = containerFixture;

    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => true;

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var connectionString = RequiresDockerAttribute.IsSupported
            ? $"{_containerFixture.GetConnectionString()}"
            : "kurrentdb://localhost:22113?tls=false";

        configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>($"Aspire:KurrentDB:Client:ConnectionString", $"{connectionString}"),
                new KeyValuePair<string, string?>($"ConnectionStrings:{key}", $"{connectionString}")
            ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<KurrentDBSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddKurrentDBClient("kurrentdb", configureSettings: configure);
        }
        else
        {
            builder.AddKeyedKurrentDBClient(key, configureSettings: configure);
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "KurrentDB": {
                                                       "Client": {
                                                         "ConnectionString": "kurrentdb://localhost:22113?tls=false",
                                                         "DisableHealthChecks": "false"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """;

    protected override (string json, string error)[] InvalidJsonToErrorMessage => new[]
        {
            ("""{"Aspire": { "KurrentDB":{ "Client": { "ConnectionString": 3 }}}}""", "Value is \"integer\" but should be \"string\"")
        };

    protected override void SetHealthCheck(KurrentDBSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(KurrentDBSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(KurrentDBSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(KurrentDBClient service)
    {
        using var source = new CancellationTokenSource(100);

        var readResult = service.ReadAllAsync(direction: Direction.Backwards, position: Position.End, maxCount: 1);

        readResult.Messages.ToArrayAsync().GetAwaiter().GetResult();
    }
}

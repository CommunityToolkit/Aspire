// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using EventStore.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.EventStore.Tests;

public class ConformanceTests(EventStoreContainerFixture containerFixture) : ConformanceTests<EventStoreClient, EventStoreSettings>, IClassFixture<EventStoreContainerFixture>
{
    private readonly EventStoreContainerFixture _containerFixture = containerFixture;

    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => true;

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var connectionString = RequiresDockerAttribute.IsSupported
            ? $"{_containerFixture.GetConnectionString()}"
            : "esdb://localhost:22113?tls=false";

        configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>($"Aspire:EventStore:Client:ConnectionString", $"{connectionString}"),
                new KeyValuePair<string, string?>($"ConnectionStrings:{key}", $"{connectionString}")
            ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<EventStoreSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddEventStoreClient("eventstore", configureSettings: configure);
        }
        else
        {
            builder.AddKeyedEventStoreClient(key, configureSettings: configure);
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "EventStore": {
                                                       "Client": {
                                                         "ConnectionString": "esdb://localhost:22113?tls=false",
                                                         "DisableHealthChecks": "false"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """;

    protected override (string json, string error)[] InvalidJsonToErrorMessage => new[]
        {
            ("""{"Aspire": { "EventStore":{ "Client": { "ConnectionString": 3 }}}}""", "Value is \"integer\" but should be \"string\"")
        };

    protected override void SetHealthCheck(EventStoreSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(EventStoreSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(EventStoreSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(EventStoreClient service)
    {
        using var source = new CancellationTokenSource(100);

        var readResult = service.ReadAllAsync(direction: Direction.Backwards, position: Position.End, maxCount: 1);

        readResult.Messages.ToArrayAsync().GetAwaiter().GetResult();
    }
}

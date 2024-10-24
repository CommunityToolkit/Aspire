// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using CommunityToolkit.Aspire.Marten;
using CommunityToolkit.Aspire.Marten.Tests;
using Marten;
using Microsoft.DotNet.RemoteExecutor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace CommunityToolkit.Aspire.Meilisearch.Tests;

public class ConformanceTests(PostgreSQLContainerFixture containerFixture) : ConformanceTests<DocumentStore, MartenSettings>, IClassFixture<PostgreSQLContainerFixture>
{
    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => "Marten";

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => true;

    protected override string? ConfigurationSectionName => "Aspire:Marten";

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var connectionString = RequiresDockerAttribute.IsSupported ?
          $"{containerFixture.GetConnectionString()}" :
          "Host=localhost;Database=test_aspire_marten1";

        configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>(CreateConfigKey("Aspire:Marten", key, "ConnectionString"), connectionString),
                new KeyValuePair<string, string?>($"ConnectionStrings:{key}", $"{connectionString}")
            ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<MartenSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddMartenClient("postgres", configureSettings: configure);
        }
        else
        {
            builder.AddKeyedMartenClient(key, configureSettings: configure);
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "Marten": {
                                                       "Client": {
                                                         "ConnectionString": "Host=localhost;Database=test_aspire_marten1"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """;

    protected override (string json, string error)[] InvalidJsonToErrorMessage => new[]
        {
            ("""{"Aspire": { "Marten":{ "Client": { "ConnectionString": 3 }}}}""", "Value is \"integer\" but should be \"string\""),
        };

    protected override void SetHealthCheck(MartenSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(MartenSettings options, bool enabled)
    {
        options.DisableMetrics = !enabled;
    }

    protected override void SetTracing(MartenSettings options, bool enabled)
    {
        options.DisableTracing = !enabled;
    }

    protected override void TriggerActivity(DocumentStore service)
    {
        using var command = new NpgsqlCommand("Select 1;");
        using var session = service.QuerySession();
        session.Execute(command);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenFeature.Contrib.Providers.GOFeatureFlag;
using OpenFeature.Model;

namespace CommunityToolkit.Aspire.GoFeatureFlag.Tests;

public class ConformanceTests : ConformanceTests<GoFeatureFlagProvider, GoFeatureFlagClientSettings>, IClassFixture<GoFeatureFlagContainerFixture>
{
    private readonly GoFeatureFlagContainerFixture _containerFixture;

    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => true;

    public ConformanceTests(GoFeatureFlagContainerFixture containerFixture)
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
                new KeyValuePair<string, string?>(CreateConfigKey("Aspire:GoFeatureFlag:Client", key, "Endpoint"), GetConnectionStringKeyValue(connectionString,"Endpoint")),
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

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<GoFeatureFlagClientSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddGoFeatureFlagClient("goff", configureSettings: configure);
        }
        else
        {
            builder.AddKeyedGoFeatureFlagClient(key, configureSettings: configure);
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "GoFeatureFlag": {
                                                       "Client": {
                                                         "Endpoint": "http://localhost:19530"
                                                       }
                                                     }
                                                   }
                                                 }
                                                 """;

    protected override (string json, string error)[] InvalidJsonToErrorMessage => new[]
        {
            ("""{"Aspire": { "GoFeatureFlag":{ "Client": { "Endpoint": 3 }}}}""", "Value is \"integer\" but should be \"string\""),
            ("""{"Aspire": { "GoFeatureFlag":{ "Client": { "Endpoint": "hello" }}}}""", "Value does not match format \"uri\"")
        };

    protected override void SetHealthCheck(GoFeatureFlagClientSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(GoFeatureFlagClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(GoFeatureFlagClientSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(GoFeatureFlagProvider service)
    {
        using var source = new CancellationTokenSource(100);

        var context = EvaluationContext.Builder()
            .Set("targetingKey", Guid.NewGuid().ToString())
            .Set("anonymous", true)
            .Build();
        service.InitializeAsync(context, source.Token).Wait();
    }
}

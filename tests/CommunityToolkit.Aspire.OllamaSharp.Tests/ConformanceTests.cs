using Aspire.Components.Common.Tests;
using Aspire.Components.ConformanceTests;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OllamaSharp;

namespace CommunityToolkit.Aspire.OllamaSharp.Tests;

public class ConformanceTests(OllamaContainerFeature ollamaContainerFeature) : ConformanceTests<IChatClient, OllamaSharpSettings>, IClassFixture<OllamaContainerFeature>
{
    protected override ServiceLifetime ServiceLifetime => ServiceLifetime.Singleton;

    protected override string ActivitySourceName => string.Empty;

    protected override string[] RequiredLogCategories => [];

    protected override bool CanConnectToServer => RequiresDockerAttribute.IsSupported;

    protected override bool SupportsKeyedRegistrations => true;

    protected override void PopulateConfiguration(ConfigurationManager configuration, string? key = null)
    {
        var connectionString = RequiresDockerAttribute.IsSupported ?
          $"{ollamaContainerFeature.GetConnectionString()}" :
          "http://localhost:11434";

        configuration.AddInMemoryCollection(
          [
              new KeyValuePair<string, string?>(CreateConfigKey("Aspire:OllamaSharp", key, "Endpoint"), connectionString),
              new KeyValuePair<string, string?>($"ConnectionStrings:{key}", connectionString)
          ]);
    }

    protected override void RegisterComponent(HostApplicationBuilder builder, Action<OllamaSharpSettings>? configure = null, string? key = null)
    {
        if (key is null)
        {
            builder.AddOllamaSharpChatClient("ollama", configure);
        }
        else
        {
            builder.AddKeyedOllamaSharpChatClient(key, configure);
        }
    }

    protected override void SetHealthCheck(OllamaSharpSettings options, bool enabled)
    {
        options.DisableHealthChecks = !enabled;
    }

    protected override void SetMetrics(OllamaSharpSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void SetTracing(OllamaSharpSettings options, bool enabled)
    {
        throw new NotImplementedException();
    }

    protected override void TriggerActivity(IChatClient service)
    {
        using var source = new CancellationTokenSource(100);

        if (service is IOllamaApiClient ollamaApiClient)
        {
            ollamaApiClient.ListLocalModelsAsync(source.Token).Wait();
        }
    }

    protected override string ValidJsonConfig => """
                                                 {
                                                   "Aspire": {
                                                     "OllamaSharp": {
                                                        "Endpoint": "http://localhost:11434"
                                                     }
                                                   }
                                                 }
                                                 """;

}
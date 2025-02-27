using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OllamaSharp;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CommunityToolkit.Aspire.OllamaSharp.Tests;

public class OllamaSharpIEmbeddingGeneratorTests
{
    private static readonly Uri Endpoint = new("https://localhost:5001/");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsFromConnectionStringCorrectly(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", $"Endpoint={Endpoint}")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedOllamaApiClient("Ollama").AddKeyedEmbeddingGenerator();
        }
        else
        {
            builder.AddOllamaApiClient("Ollama").AddEmbeddingGenerator();
        }

        using var host = builder.Build();

        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("Ollama") :
            host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.NotNull(client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri);
        Assert.Equal(Endpoint, client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanSetConnectionStringInCode(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", "Endpoint=http://not-used")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedOllamaApiClient("Ollama", settings => settings.Endpoint = Endpoint).AddKeyedEmbeddingGenerator(); ;
        }
        else
        {
            builder.AddOllamaApiClient("Ollama", settings => settings.Endpoint = Endpoint).AddEmbeddingGenerator(); ;
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("Ollama") :
            host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.NotNull(client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri);
        Assert.Equal(Endpoint, client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri);
        Assert.DoesNotContain("http://not-used", client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri?.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConnectionStringWinsOverConfigSection(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Aspire:Ollama:Ollama:ConnectionString", "Endpoint=http://not-used"),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", $"Endpoint={Endpoint}")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedOllamaApiClient("Ollama").AddKeyedEmbeddingGenerator();
        }
        else
        {
            builder.AddOllamaApiClient("Ollama").AddEmbeddingGenerator();
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("Ollama") :
            host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.NotNull(client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri);
        Assert.Equal(Endpoint, client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri);
        Assert.DoesNotContain("http://not-used", client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri?.ToString());
    }

    [Fact]
    public void CanSetMultipleKeyedClients()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", $"Endpoint={Endpoint}"),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama2", "Endpoint=https://localhost:5002/"),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama3", "Endpoint=https://localhost:5003/")
        ]);

        builder.AddOllamaApiClient("Ollama").AddEmbeddingGenerator();
        builder.AddKeyedOllamaApiClient("Ollama2").AddKeyedEmbeddingGenerator();
        builder.AddKeyedOllamaApiClient("Ollama3").AddKeyedEmbeddingGenerator();

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var client2 = host.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("Ollama2");
        var client3 = host.Services.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("Ollama3");

        Assert.Equal(Endpoint, client.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri);
        Assert.Equal("https://localhost:5002/", client2.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri?.ToString());
        Assert.Equal("https://localhost:5003/", client3.GetService<EmbeddingGeneratorMetadata>()?.ProviderUri?.ToString());

        Assert.NotEqual(client, client2);
        Assert.NotEqual(client, client3);
    }

    [Fact]
    public void CanChainUseMethodsCorrectly()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama",$"Endpoint={Endpoint}")
        ]);

        builder.Services.AddDistributedMemoryCache();

        builder.AddOllamaApiClient("Ollama")
            .AddEmbeddingGenerator()
            .UseDistributedCache();

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        var distributedCacheClient = Assert.IsType<DistributedCachingEmbeddingGenerator<string, Embedding<float>>>(client);
        var otelClient = Assert.IsType<OpenTelemetryEmbeddingGenerator<string, Embedding<float>>>(GetInnerGenerator(distributedCacheClient));

        Assert.IsType<IOllamaApiClient>(GetInnerGenerator(otelClient), exactMatch: false);
    }

    private static IEmbeddingGenerator<TInput, TEmbedding> GetInnerGenerator<TInput, TEmbedding>(DelegatingEmbeddingGenerator<TInput, TEmbedding> generator)
        where TEmbedding : Embedding =>
        (IEmbeddingGenerator<TInput,TEmbedding>)(generator.GetType()
            .GetProperty("InnerGenerator", BindingFlags.Instance | BindingFlags.NonPublic)?
            .GetValue(generator, null) ?? throw new InvalidOperationException());
}

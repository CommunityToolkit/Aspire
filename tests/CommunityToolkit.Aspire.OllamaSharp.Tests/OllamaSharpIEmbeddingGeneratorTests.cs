using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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

        Assert.NotNull(client.Metadata.ProviderUri);
        Assert.Equal(Endpoint, client.Metadata.ProviderUri);
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

        Assert.NotNull(client.Metadata.ProviderUri);
        Assert.Equal(Endpoint, client.Metadata.ProviderUri);
        Assert.DoesNotContain("http://not-used", client.Metadata.ProviderUri.ToString());
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

        Assert.NotNull(client.Metadata.ProviderUri);
        Assert.Equal(Endpoint, client.Metadata.ProviderUri);
        Assert.DoesNotContain("http://not-used", client.Metadata.ProviderUri.ToString());
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

        Assert.Equal(Endpoint, client.Metadata.ProviderUri);
        Assert.Equal("https://localhost:5002/", client2.Metadata.ProviderUri?.ToString());
        Assert.Equal("https://localhost:5003/", client3.Metadata.ProviderUri?.ToString());

        Assert.NotEqual(client, client2);
        Assert.NotEqual(client, client3);
    }
}

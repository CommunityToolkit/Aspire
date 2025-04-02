using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OllamaSharp;

namespace CommunityToolkit.Aspire.OllamaSharp.Tests;

public class OllamaApiClientTests
{
    private readonly static Uri Endpoint = new("https://localhost:5001/");

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
            builder.AddKeyedOllamaApiClient("Ollama");
        }
        else
        {
            builder.AddOllamaApiClient("Ollama");
        }

        using var host = builder.Build();

        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama") :
            host.Services.GetRequiredService<IOllamaApiClient>();

        Assert.NotNull(client.Uri);
        Assert.Equal(Endpoint, client.Uri);
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
            builder.AddKeyedOllamaApiClient("Ollama", settings => settings.Endpoint = Endpoint);
        }
        else
        {
            builder.AddOllamaApiClient("Ollama", settings => settings.Endpoint = Endpoint);
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama") :
            host.Services.GetRequiredService<IOllamaApiClient>();

        Assert.NotNull(client.Uri);
        Assert.Equal(Endpoint, client.Uri);
        Assert.DoesNotContain("http://not-used", client.Uri.ToString());
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
            builder.AddKeyedOllamaApiClient("Ollama");
        }
        else
        {
            builder.AddOllamaApiClient("Ollama");
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama") :
            host.Services.GetRequiredService<IOllamaApiClient>();

        Assert.NotNull(client.Uri);
        Assert.Equal(Endpoint, client.Uri);
        Assert.DoesNotContain("http://not-used", client.Uri.ToString());
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

        builder.AddOllamaApiClient("Ollama");
        builder.AddKeyedOllamaApiClient("Ollama2");
        builder.AddKeyedOllamaApiClient("Ollama3");

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IOllamaApiClient>();
        var client2 = host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama2");
        var client3 = host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama3");

        Assert.Equal(Endpoint, client.Uri);
        Assert.Equal("https://localhost:5002/", client2.Uri?.ToString());
        Assert.Equal("https://localhost:5003/", client3.Uri?.ToString());

        Assert.NotEqual(client, client2);
        Assert.NotEqual(client, client3);
    }

    [Fact]
    public void RegisteringChatClientAndEmbeddingGeneratorReturnsCorrectModelForServices()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Chat", $"Endpoint={Endpoint};Model=Chat"),
            new KeyValuePair<string, string?>("ConnectionStrings:Embedding", $"Endpoint={Endpoint};Model=Embedding")
        ]);

        builder.AddOllamaApiClient("Chat").AddChatClient();
        builder.AddOllamaApiClient("Embedding").AddEmbeddingGenerator();
        using var host = builder.Build();

        var chatClient = host.Services.GetRequiredService<IChatClient>();
        var embeddingGenerator = host.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.Equal("Chat", chatClient.GetService<ChatClientMetadata>()?.DefaultModelId);
        Assert.Equal("Embedding", embeddingGenerator.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId);
    }

    [Fact]
    public void RegisteringChatClientAndEmbeddingGeneratorResultsInMultipleOllamaApiClients()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Chat", $"Endpoint={Endpoint};Model=Chat"),
            new KeyValuePair<string, string?>("ConnectionStrings:Embedding", $"Endpoint={Endpoint};Model=Embedding")
        ]);

        builder.AddOllamaApiClient("Chat").AddChatClient();
        builder.AddOllamaApiClient("Embedding").AddEmbeddingGenerator();
        using var host = builder.Build();

        var ollamaApiClients = host.Services.GetServices<IOllamaApiClient>();

        Assert.Equal(2, ollamaApiClients.Count());
    }
}

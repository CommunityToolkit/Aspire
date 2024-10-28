using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.OllamaSharp.Tests;

public class AspireOllamaSharpExtensionsTests
{
    private const string Endpoint = "https://localhost:5001/";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadsFromConnectionStringCorrectly(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", Endpoint)
        ]);

        if (useKeyed)
        {
            builder.AddKeyedOllamaSharpClient("Ollama");
        }
        else
        {
            builder.AddOllamaSharpClient("Ollama");
        }

        using var host = builder.Build();

        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

        Assert.NotNull(client.Metadata.ProviderUri);
        Assert.Equal(Endpoint, client.Metadata.ProviderUri.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CanSetConnectionStringInCode(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", "http://not-used")
        ]);

        if (useKeyed)
        {
            builder.AddKeyedOllamaSharpClient("Ollama", settings => settings.ConnectionString = Endpoint);
        }
        else
        {
            builder.AddOllamaSharpClient("Ollama", settings => settings.ConnectionString = Endpoint);
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

        Assert.NotNull(client.Metadata.ProviderUri);
        Assert.Equal(Endpoint, client.Metadata.ProviderUri.ToString());
        Assert.DoesNotContain("http://not-used", client.Metadata.ProviderUri.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConnectionStringWinsOverConfigSection(bool useKeyed)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("Aspire:Ollama:Ollama:ConnectionString", "http://not-used"),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", Endpoint)
        ]);

        if (useKeyed)
        {
            builder.AddKeyedOllamaSharpClient("Ollama");
        }
        else
        {
            builder.AddOllamaSharpClient("Ollama");
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

        Assert.NotNull(client.Metadata.ProviderUri);
        Assert.Equal(Endpoint, client.Metadata.ProviderUri.ToString());
        Assert.DoesNotContain("http://not-used", client.Metadata.ProviderUri.ToString());
    }

    [Fact]
    public void CanSetMultipleKeyedClients()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama", Endpoint),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama2", "https://localhost:5002/"),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama3", "https://localhost:5003/")
        ]);

        builder.AddOllamaSharpClient("Ollama");
        builder.AddKeyedOllamaSharpClient("Ollama2");
        builder.AddKeyedOllamaSharpClient("Ollama3");

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IChatClient>();
        var client2 = host.Services.GetRequiredKeyedService<IChatClient>("Ollama2");
        var client3 = host.Services.GetRequiredKeyedService<IChatClient>("Ollama3");

        Assert.Equal(Endpoint, client.Metadata.ProviderUri?.ToString());
        Assert.Equal("https://localhost:5002/", client2.Metadata.ProviderUri?.ToString());
        Assert.Equal("https://localhost:5003/", client3.Metadata.ProviderUri?.ToString());

        Assert.NotEqual(client, client2);
        Assert.NotEqual(client, client3);
    }
}

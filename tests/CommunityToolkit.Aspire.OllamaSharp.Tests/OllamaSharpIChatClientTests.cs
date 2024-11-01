using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.OllamaSharp.Tests;

public class OllamaSharpIChatClientTests
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
            builder.AddKeyedOllamaSharpChatClient("Ollama");
        }
        else
        {
            builder.AddOllamaSharpChatClient("Ollama");
        }

        using var host = builder.Build();

        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

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
            builder.AddKeyedOllamaSharpChatClient("Ollama", settings => settings.Endpoint = Endpoint);
        }
        else
        {
            builder.AddOllamaSharpChatClient("Ollama", settings => settings.Endpoint = Endpoint);
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

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
            builder.AddKeyedOllamaSharpChatClient("Ollama");
        }
        else
        {
            builder.AddOllamaSharpChatClient("Ollama");
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

        Assert.NotNull(client.Metadata.ProviderUri);
        Assert.Equal(Endpoint, client.Metadata.ProviderUri);
        Assert.DoesNotContain("http://not-used", client.Metadata.ProviderUri.ToString());
    }

    [Fact]
    public void CanSetMultipleKeyedClients()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama",$"Endpoint={Endpoint}"),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama2", "Endpoint=https://localhost:5002/"),
            new KeyValuePair<string, string?>("ConnectionStrings:Ollama3", "Endpoint=https://localhost:5003/")
        ]);

        builder.AddOllamaSharpChatClient("Ollama");
        builder.AddKeyedOllamaSharpChatClient("Ollama2");
        builder.AddKeyedOllamaSharpChatClient("Ollama3");

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IChatClient>();
        var client2 = host.Services.GetRequiredKeyedService<IChatClient>("Ollama2");
        var client3 = host.Services.GetRequiredKeyedService<IChatClient>("Ollama3");

        Assert.Equal(Endpoint, client.Metadata.ProviderUri);
        Assert.Equal("https://localhost:5002/", client2.Metadata.ProviderUri?.ToString());
        Assert.Equal("https://localhost:5003/", client3.Metadata.ProviderUri?.ToString());

        Assert.NotEqual(client, client2);
        Assert.NotEqual(client, client3);
    }
}

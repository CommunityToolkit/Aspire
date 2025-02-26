using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OllamaSharp;
using System.Runtime.CompilerServices;

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
            builder.AddKeyedOllamaApiClient("Ollama").AddKeyedChatClient();
        }
        else
        {
            builder.AddOllamaApiClient("Ollama").AddChatClient();
        }

        using var host = builder.Build();

        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

        Assert.NotNull(client.GetService<ChatClientMetadata>()?.ProviderUri);
        Assert.Equal(Endpoint, client.GetService<ChatClientMetadata>()?.ProviderUri);
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
            builder.AddKeyedOllamaApiClient("Ollama", settings => settings.Endpoint = Endpoint).AddKeyedChatClient();
        }
        else
        {
            builder.AddOllamaApiClient("Ollama", settings => settings.Endpoint = Endpoint).AddChatClient();
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

        Assert.NotNull(client.GetService<ChatClientMetadata>()?.ProviderUri);
        Assert.Equal(Endpoint, client.GetService<ChatClientMetadata>()?.ProviderUri);
        Assert.DoesNotContain("http://not-used", client.GetService<ChatClientMetadata>()?.ProviderUri?.ToString());
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
            builder.AddKeyedOllamaApiClient("Ollama").AddKeyedChatClient();
        }
        else
        {
            builder.AddOllamaApiClient("Ollama").AddChatClient();
        }

        using var host = builder.Build();
        var client = useKeyed ?
            host.Services.GetRequiredKeyedService<IChatClient>("Ollama") :
            host.Services.GetRequiredService<IChatClient>();

        Assert.NotNull(client.GetService<ChatClientMetadata>()?.ProviderUri);
        Assert.Equal(Endpoint, client.GetService<ChatClientMetadata>()?.ProviderUri);
        Assert.DoesNotContain("http://not-used", client.GetService<ChatClientMetadata>()?.ProviderUri?.ToString());
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

        builder.AddOllamaApiClient("Ollama").AddChatClient();
        builder.AddKeyedOllamaApiClient("Ollama2").AddKeyedChatClient();
        builder.AddKeyedOllamaApiClient("Ollama3").AddKeyedChatClient();

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IChatClient>();
        var client2 = host.Services.GetRequiredKeyedService<IChatClient>("Ollama2");
        var client3 = host.Services.GetRequiredKeyedService<IChatClient>("Ollama3");

        Assert.Equal(Endpoint, client.GetService<ChatClientMetadata>()?.ProviderUri);
        Assert.Equal("https://localhost:5002/", client2.GetService<ChatClientMetadata>()?.ProviderUri?.ToString());
        Assert.Equal("https://localhost:5003/", client3.GetService<ChatClientMetadata>()?.ProviderUri?.ToString());

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
            .AddChatClient()
            .UseDistributedCache()
            .UseFunctionInvocation();

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<IChatClient>();
        
        var distributedCacheClient = Assert.IsType<DistributedCachingChatClient>(client);
        var functionInvocationClient = Assert.IsType<FunctionInvokingChatClient>(GetInnerClient(distributedCacheClient));
        var otelClient = Assert.IsType<OpenTelemetryChatClient>(GetInnerClient(functionInvocationClient));
        
        Assert.IsType<IOllamaApiClient>(GetInnerClient(otelClient), exactMatch: false);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_InnerClient")]
    private static extern IChatClient GetInnerClient(DelegatingChatClient client);
}

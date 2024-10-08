﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OllamaSharp;

namespace Aspire.CommunityToolkit.OllamaSharp.Tests;

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
            builder.AddKeyedOllamaApiClient("Ollama");
        }
        else
        {
            builder.AddOllamaApiClient("Ollama");
        }

        using var host = builder.Build();

        var client = useKeyed ?
            (OllamaApiClient)host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama") :
            (OllamaApiClient)host.Services.GetRequiredService<IOllamaApiClient>();
        Assert.Equal(Endpoint, client.Config.Uri.ToString());
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
            builder.AddKeyedOllamaApiClient("Ollama", settings => settings.ConnectionString = Endpoint);
        }
        else
        {
            builder.AddOllamaApiClient("Ollama", settings => settings.ConnectionString = Endpoint);
        }

        using var host = builder.Build();
        var client = useKeyed ?
            (OllamaApiClient)host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama") :
            (OllamaApiClient)host.Services.GetRequiredService<IOllamaApiClient>();

        Assert.Equal(Endpoint, client.Config.Uri.ToString());
        Assert.DoesNotContain("http://not-used", client.Config.Uri.ToString());
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
            builder.AddKeyedOllamaApiClient("Ollama");
        }
        else
        {
            builder.AddOllamaApiClient("Ollama");
        }

        using var host = builder.Build();
        var client = useKeyed ?
            (OllamaApiClient)host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama") :
            (OllamaApiClient)host.Services.GetRequiredService<IOllamaApiClient>();

        Assert.Equal(Endpoint, client.Config.Uri.ToString());
        Assert.DoesNotContain("http://not-used", client.Config.Uri.ToString());
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

        builder.AddOllamaApiClient("Ollama");
        builder.AddKeyedOllamaApiClient("Ollama2");
        builder.AddKeyedOllamaApiClient("Ollama3");

        using var host = builder.Build();
        var client = (OllamaApiClient)host.Services.GetRequiredService<IOllamaApiClient>();
        var client2 = (OllamaApiClient)host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama2");
        var client3 = (OllamaApiClient)host.Services.GetRequiredKeyedService<IOllamaApiClient>("Ollama3");

        Assert.Equal(Endpoint, client.Config.Uri.ToString());
        Assert.Equal("https://localhost:5002/", client2.Config.Uri.ToString());
        Assert.Equal("https://localhost:5003/", client3.Config.Uri.ToString());

        Assert.NotEqual(client, client2);
        Assert.NotEqual(client, client3);
    }
}

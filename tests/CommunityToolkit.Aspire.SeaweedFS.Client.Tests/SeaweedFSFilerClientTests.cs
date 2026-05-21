using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CommunityToolkit.Aspire.SeaweedFS.Client.Tests;

public class SeaweedFSFilerClientTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
    {
        // Validates the primary constructor runtime check
        var exception = Assert.Throws<ArgumentNullException>(() => new SeaweedFSFilerClient(null!));
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSFilerClient_ThrowsArgumentNullException_WhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        void action() => builder.AddSeaweedFSFilerClient("seaweedfs");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSFilerClient_ThrowsArgumentException_WhenConnectionNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        void action() => builder.AddSeaweedFSFilerClient(null!);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("connectionName", exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSFilerClient_RegistersHttpClientWithCorrectBaseAddress()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:seaweedfs", "FilerEndpoint=http://localhost:8888" }
        });

        builder.AddSeaweedFSFilerClient("seaweedfs");

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<SeaweedFSFilerClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.HttpClient);
        Assert.Equal("http://localhost:8888/", client.HttpClient.BaseAddress?.ToString());
    }

    [Fact]
    public void AddSeaweedFSFilerClient_FallbackToStandardEndpoint_WhenFilerEndpointIsMissing()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Simulates a legacy or simplified connection string where only 'Endpoint' is defined
            { "ConnectionStrings:seaweedfs", "Endpoint=http://localhost:9999" }
        });

        builder.AddSeaweedFSFilerClient("seaweedfs");

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<SeaweedFSFilerClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.HttpClient);
        Assert.Equal("http://localhost:9999/", client.HttpClient.BaseAddress?.ToString());
    }

    [Fact]
    public void AddSeaweedFSFilerClient_UsesServiceDiscovery_WhenNoEndpointIsConfigured()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        // No connection string or endpoint is added to the configuration.
        // It must fallback seamlessly to the Aspire Service Discovery protocol.
        builder.AddSeaweedFSFilerClient("my-filer-cluster");

        using var host = builder.Build();
        var client = host.Services.GetRequiredService<SeaweedFSFilerClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.HttpClient);
        Assert.Equal("http://my-filer-cluster/", client.HttpClient.BaseAddress?.ToString());
    }
}
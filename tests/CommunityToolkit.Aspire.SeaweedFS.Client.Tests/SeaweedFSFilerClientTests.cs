using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.SeaweedFS.Client.Tests;

public class SeaweedFSFilerClientTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
    {
        // Validates the primary constructor runtime check
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new SeaweedFSFilerClient(null!));
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSFilerClient_ThrowsArgumentNullException_WhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        void action() => builder.AddSeaweedFSFilerClient("seaweedfs");

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSFilerClient_ThrowsArgumentException_WhenConnectionNameIsNull()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        void action() => builder.AddSeaweedFSFilerClient(null!);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal("connectionName", exception.ParamName);
    }

    [Fact]
    public void AddSeaweedFSFilerClient_RegistersHttpClientWithCorrectBaseAddress()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:seaweedfs", "FilerEndpoint=http://localhost:8888" }
        });

        builder.AddSeaweedFSFilerClient("seaweedfs");

        using IHost host = builder.Build();
        SeaweedFSFilerClient client = host.Services.GetRequiredService<SeaweedFSFilerClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.HttpClient);
        Assert.Equal("http://localhost:8888/", client.HttpClient.BaseAddress?.ToString());
    }

    [Fact]
    public void AddSeaweedFSFilerClient_FallbackToStandardEndpoint_WhenFilerEndpointIsMissing()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Simulates a legacy or simplified connection string where only 'Endpoint' is defined
            { "ConnectionStrings:seaweedfs", "Endpoint=http://localhost:9999" }
        });

        builder.AddSeaweedFSFilerClient("seaweedfs");

        using IHost host = builder.Build();
        SeaweedFSFilerClient client = host.Services.GetRequiredService<SeaweedFSFilerClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.HttpClient);
        Assert.Equal("http://localhost:9999/", client.HttpClient.BaseAddress?.ToString());
    }

    [Fact]
    public void AddSeaweedFSFilerClient_UsesServiceDiscovery_WhenNoEndpointIsConfigured()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);

        // No connection string or endpoint is added to the configuration.
        // It must fallback seamlessly to the Aspire Service Discovery protocol.
        builder.AddSeaweedFSFilerClient("my-filer-cluster");

        using IHost host = builder.Build();
        SeaweedFSFilerClient client = host.Services.GetRequiredService<SeaweedFSFilerClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.HttpClient);
        Assert.Equal("http://my-filer-cluster/", client.HttpClient.BaseAddress?.ToString());
    }
}
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.VLLM.Tests;

public class AddVLLMClientTests
{
    private const string ConnectionName = "vllm";
    private const string Endpoint = "http://localhost:8000";
    private const string Model = "Qwen/Qwen3-0.6B";

    [Fact]
    public void AddChatClientRegistersResolvableChatClient()
    {
        var builder = CreateBuilder();
        builder.AddVLLMClient(ConnectionName, s => s.Model = Model).AddChatClient();

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetRequiredService<IChatClient>());
    }

    [Fact]
    public void AddChatClientAppendsV1AndUsesServedModel()
    {
        var builder = CreateBuilder();
        builder.AddVLLMClient(ConnectionName, s =>
        {
            s.Model = Model;
            s.DisableTracing = true;
        }).AddChatClient();

        using var host = builder.Build();
        var metadata = host.Services.GetRequiredService<IChatClient>().GetService<ChatClientMetadata>();

        Assert.NotNull(metadata);
        Assert.Equal($"{Endpoint}/v1", metadata.ProviderUri!.AbsoluteUri.TrimEnd('/'));
        Assert.Equal(Model, metadata.DefaultModelId);
    }

    [Fact]
    public void AddChatClientModelIdOverridesSettings()
    {
        var builder = CreateBuilder();
        builder.AddVLLMClient(ConnectionName, s =>
        {
            s.Model = "from-settings";
            s.DisableTracing = true;
        }).AddChatClient("from-argument");

        using var host = builder.Build();
        var metadata = host.Services.GetRequiredService<IChatClient>().GetService<ChatClientMetadata>();

        Assert.Equal("from-argument", metadata?.DefaultModelId);
    }

    [Fact]
    public void AddChatClientWithoutModelThrowsOnResolve()
    {
        var builder = CreateBuilder();
        builder.AddVLLMClient(ConnectionName).AddChatClient();

        using var host = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<IChatClient>());
        Assert.Contains("Model", exception.Message);
    }

    [Fact]
    public void AddChatClientWithoutApiKeyUsesPlaceholder()
    {
        var builder = CreateBuilder();
        var clientBuilder = builder.AddVLLMClient(ConnectionName, s => s.Model = Model);
        clientBuilder.AddChatClient();

        using var host = builder.Build();

        Assert.Null(clientBuilder.Settings.Key);
        Assert.Null(Record.Exception(() => host.Services.GetRequiredService<IChatClient>()));
    }

    [Fact]
    public void AddVLLMClientResolvesEndpointFromConnectionString()
    {
        var builder = CreateBuilder();
        var clientBuilder = builder.AddVLLMClient(ConnectionName);

        Assert.Equal(new Uri(Endpoint), clientBuilder.Settings.Endpoint);
        Assert.Equal(ConnectionName, clientBuilder.ConnectionName);
        Assert.Null(clientBuilder.ServiceKey);
    }

    [Fact]
    public void AddVLLMClientBindsSettingsFromConfiguration()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Aspire:VLLM:DisableTracing"] = "true",
            [$"Aspire:VLLM:{ConnectionName}:Model"] = Model,
            [$"Aspire:VLLM:{ConnectionName}:Key"] = "from-config",
        });

        var clientBuilder = builder.AddVLLMClient(ConnectionName);

        Assert.Equal(Model, clientBuilder.Settings.Model);
        Assert.Equal("from-config", clientBuilder.Settings.Key);
        Assert.True(clientBuilder.Settings.DisableTracing);
        Assert.True(clientBuilder.DisableTracing);
    }

    [Fact]
    public void AddVLLMClientConfigureCallbackOverridesConfiguration()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            [$"Aspire:VLLM:{ConnectionName}:Model"] = "from-config",
        });

        var clientBuilder = builder.AddVLLMClient(ConnectionName, s => s.Model = "from-callback");

        Assert.Equal("from-callback", clientBuilder.Settings.Model);
    }

    [Fact]
    public void AddVLLMClientMissingEndpointThrows()
    {
        var builder = CreateBuilder(connectionString: null);

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddVLLMClient(ConnectionName));

        Assert.Contains(ConnectionName, exception.Message);
    }

    [Fact]
    public void AddVLLMClientRegistersHealthCheckByDefault()
    {
        var builder = CreateBuilder();
        builder.AddVLLMClient(ConnectionName, s => s.HealthCheckTimeout = 1500);

        using var host = builder.Build();

        var registration = Assert.Single(GetHealthCheckRegistrations(host), r => r.Name == "VLLM");
        Assert.Equal(TimeSpan.FromMilliseconds(1500), registration.Timeout);
    }

    [Fact]
    public void AddVLLMClientDisableHealthChecksSkipsRegistration()
    {
        var builder = CreateBuilder();
        builder.AddVLLMClient(ConnectionName, s => s.DisableHealthChecks = true);

        using var host = builder.Build();

        Assert.DoesNotContain(GetHealthCheckRegistrations(host), r => r.Name == "VLLM");
    }

    [Fact]
    public void AddKeyedVLLMClientRegistersKeyedChatClientAndHealthCheck()
    {
        var builder = CreateBuilder();
        builder.AddKeyedVLLMClient(ConnectionName, s => s.Model = Model).AddKeyedChatClient();

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetRequiredKeyedService<IChatClient>(ConnectionName));
        Assert.Null(host.Services.GetService<IChatClient>());
        Assert.Contains(GetHealthCheckRegistrations(host), r => r.Name == $"VLLM_{ConnectionName}");
    }

    [Fact]
    public void AddKeyedVLLMClientWithServiceKeyUsesServiceKey()
    {
        var builder = CreateBuilder();
        var clientBuilder = builder.AddKeyedVLLMClient("chat", ConnectionName, s => s.Model = Model);
        clientBuilder.AddKeyedChatClient();

        using var host = builder.Build();

        Assert.Equal("chat", clientBuilder.ServiceKey);
        Assert.NotNull(host.Services.GetRequiredKeyedService<IChatClient>("chat"));
    }

    [Fact]
    public void AddKeyedChatClientOnNonKeyedClientThrows()
    {
        var builder = CreateBuilder();
        var clientBuilder = builder.AddVLLMClient(ConnectionName, s => s.Model = Model);

        Assert.Throws<InvalidOperationException>(() => clientBuilder.AddKeyedChatClient());
    }

    private static HostApplicationBuilder CreateBuilder(
        Dictionary<string, string?>? config = null,
        string? connectionString = Endpoint)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        var values = new Dictionary<string, string?>(config ?? []);
        if (connectionString is not null)
        {
            values[$"ConnectionStrings:{ConnectionName}"] = $"Endpoint={connectionString}";
        }

        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    private static ICollection<HealthCheckRegistration> GetHealthCheckRegistrations(IHost host) =>
        host.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
}

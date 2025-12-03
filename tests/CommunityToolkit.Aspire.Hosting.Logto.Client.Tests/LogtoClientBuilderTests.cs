using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Logto.Client.Tests;

public class LogtoClientBuilderTests
{
    [Fact]
    public void AddLogtoSDKClient_ThrowsArgumentNull_WhenBuilderIsNull()
    {
        IHostApplicationBuilder? builder = null;
        
        var ex = Assert.Throws<ArgumentNullException>(() =>
            builder!.AddLogtoSDKClient());
        
        Assert.Equal("builder", ex.ParamName);
    }

    [Fact]
    public void AddLogtoSDKClient_ThrowsInvalidOperation_WhenEndpointNotConfiguredAnywhere()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            //empty
        });
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLogtoSDKClient());

        Assert.Contains("Logto Endpoint must be configured", ex.Message);
    }

    [Fact]
    public void AddLogtoSDKClient_UsesEndpointFromConfiguration_WhenPresent()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = "https://logto-config.example.com",
            ["Aspire:Logto:Client:AppId"] = "test-app-id",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
        });
        
        builder.AddLogtoSDKClient();
        
        var host = builder.Build();
        Assert.NotNull(host);
    }

    [Fact]
    public void AddLogtoSDKClient_UsesEndpointFromConnectionString_WhenConfigDoesNotContainEndpoint()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:AppId"] = "test-app-id",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
            ["ConnectionStrings:Logto"] = "Endpoint=https://logto-from-cs.example.com"
        });
        
        builder.AddLogtoSDKClient(connectionName: "Logto");
        
        var host = builder.Build();
        Assert.NotNull(host);
    }

    [Fact]
    public void AddLogtoSDKClient_ThrowsInvalidOperation_WhenConfigureSettingsClearsEndpoint()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = "https://logto-config.example.com",
            ["Aspire:Logto:Client:AppId"] = "test-app-id",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
        });
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLogtoSDKClient(configureSettings: opt =>
            {
                // Кто-то в конфиге убил Endpoint
                opt.Endpoint = "   ";
            }));
        
        Assert.Equal("Logto Endpoint must be configured.", ex.Message);
    }

    [Fact]
    public void AddLogtoSDKClient_ThrowsInvalidOperation_WhenAppIdIsMissingAfterConfigureSettings()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = "https://logto-config.example.com",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
        });
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLogtoSDKClient(configureSettings: opt =>
            {
                //empty
            }));

        Assert.Equal("Logto AppId must be configured.", ex.Message);
    }
}
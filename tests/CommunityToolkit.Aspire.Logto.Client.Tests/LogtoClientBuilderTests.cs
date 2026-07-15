using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Logto.Client.Tests;

public class LogtoClientBuilderTests
{
    [Fact]
    public void AddLogtoOIDC_ThrowsArgumentNull_WhenBuilderIsNull()
    {
        IHostApplicationBuilder? builder = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            builder!.AddLogtoOIDC());

        Assert.Equal("builder", ex.ParamName);
    }

    [Fact]
    public void AddLogtoOIDC_ThrowsInvalidOperation_WhenEndpointNotConfiguredAnywhere()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            //empty
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLogtoOIDC());

        Assert.Contains("Logto Endpoint must be configured", ex.Message);
    }

    [Fact]
    public void AddLogtoOIDC_UsesEndpointFromConfiguration_WhenPresent()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = "https://logto-config.example.com",
            ["Aspire:Logto:Client:AppId"] = "test-app-id",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
        });

        builder.AddLogtoOIDC();

        var host = builder.Build();
        Assert.NotNull(host);
    }

    [Fact]
    public void AddLogtoOIDC_UsesEndpointFromConnectionString_WhenConfigDoesNotContainEndpoint()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:AppId"] = "test-app-id",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
            ["ConnectionStrings:Logto"] = "Endpoint=https://logto-from-cs.example.com"
        });

        builder.AddLogtoOIDC(connectionName: "Logto");

        var host = builder.Build();
        Assert.NotNull(host);
    }

    [Fact]
    public void AddLogtoOIDC_ThrowsInvalidOperation_WhenConfigureSettingsClearsEndpoint()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = "https://logto-config.example.com",
            ["Aspire:Logto:Client:AppId"] = "test-app-id",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLogtoOIDC(logtoOptions: opt =>
            {
                opt.Endpoint = "   ";
            }));

        Assert.Equal("Logto Endpoint must be configured.", ex.Message);
    }

    [Fact]
    public void AddLogtoOIDC_ThrowsInvalidOperation_WhenAppIdIsMissingAfterConfigureSettings()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = "https://logto-config.example.com",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret",
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.AddLogtoOIDC(logtoOptions: _ =>
            {
                //empty
            }));

        Assert.Equal("Logto AppId must be configured.", ex.Message);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://logto.example.com")]
    public void AddLogtoOIDC_ThrowsInvalidOperation_WhenEndpointIsNotHttp(string endpoint)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = endpoint,
            ["Aspire:Logto:Client:AppId"] = "test-app-id"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddLogtoOIDC());

        Assert.Equal("Logto Endpoint must be an absolute HTTP or HTTPS URI.", ex.Message);
    }
}

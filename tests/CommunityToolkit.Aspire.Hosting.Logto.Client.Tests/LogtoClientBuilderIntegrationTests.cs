using Logto.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Hosting.Logto.Client.Tests;

public class LogtoClientBuilderIntegrationTests
{
    private static WebApplicationBuilder CreateBuilderWithBaseConfig(
        Dictionary<string, string?>? extraConfig = null)
    {
        var builder = WebApplication.CreateBuilder();

        var config = new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = "https://logto.example.com",
            ["Aspire:Logto:Client:AppId"] = "test-app-id",
            ["Aspire:Logto:Client:AppSecret"] = "test-secret"
        };

        if (extraConfig is not null)
        {
            foreach (var kv in extraConfig)
            {
                config[kv.Key] = kv.Value;
            }
        }

        builder.Configuration.AddInMemoryCollection(config);

        return builder;
    }

    [Fact]
    public async Task AddLogtoSDKClient_RegistersLogtoAuthenticationScheme()
    {
        // Arrange
        var builder = CreateBuilderWithBaseConfig();

        // Act
        builder.AddLogtoSDKClient();
        using var host = builder.Build();

        // Assert
        var schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync("Logto");

        Assert.NotNull(scheme);
        Assert.Equal("Logto", scheme!.Name);
    }

    [Fact]
    public async Task AddLogtoSDKClient_AllowsOverrideOfAuthenticationScheme()
    {
        // Arrange
        var builder = CreateBuilderWithBaseConfig();
        const string customScheme = "MyLogto";

        // Act
        builder.AddLogtoSDKClient(authenticationScheme: customScheme);
        using var host = builder.Build();

        // Assert
        var schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync(customScheme);

        Assert.NotNull(scheme);
        Assert.Equal(customScheme, scheme!.Name);
    }

    [Fact]
    public void AddLogtoSDKClient_UsesConnectionStringEndpoint_WhenSectionEndpointMissing()
    {
        // Arrange: удаляем Endpoint из секции, оставляем только в connection string
        var extraConfig = new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = null,
            ["ConnectionStrings:Logto"] = "Endpoint=https://logto-from-cs.example.com"
        };

        var builder = CreateBuilderWithBaseConfig(extraConfig);

        // Act
        builder.AddLogtoSDKClient(connectionName: "Logto");
        using var host = builder.Build();

        // Assert: как минимум убедимся, что всё собралось
        // и LogtoOptions вообще зарегистрированы (если библиотека их регистрирует)
        var optionsMonitor = host.Services.GetService<IOptionsMonitor<LogtoOptions>>();
        Assert.NotNull(optionsMonitor);

        var options = optionsMonitor!.Get("Logto"); // имя схемы
        Assert.StartsWith("https://logto-from-cs.example.com", options.Endpoint);
        Assert.Equal("test-app-id", options.AppId);
        Assert.Equal("test-secret", options.AppSecret);
    }

    [Fact]
    public void AddLogtoSDKClient_ConfigureSettings_CanOverrideOptions()
    {
        // Arrange
        var builder = CreateBuilderWithBaseConfig();

        // Act
        builder.AddLogtoSDKClient(configureSettings: opt =>
        {
            opt.Endpoint = "https://overridden.example.com";
            opt.AppId = "overridden-app-id";
        });

        using var host = builder.Build();

        // Assert
        var optionsMonitor = host.Services.GetService<IOptionsMonitor<LogtoOptions>>();
        Assert.NotNull(optionsMonitor);

        var options = optionsMonitor!.Get("Logto");
        Assert.StartsWith("https://overridden.example.com", options.Endpoint);
        Assert.Equal("overridden-app-id", options.AppId);
    }
}
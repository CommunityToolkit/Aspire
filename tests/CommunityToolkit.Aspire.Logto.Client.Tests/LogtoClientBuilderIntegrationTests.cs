using Logto.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Logto.Client.Tests;

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
    public async Task AddLogtoOIDC_RegistersLogtoAuthenticationScheme()
    {
        // Arrange
        var builder = CreateBuilderWithBaseConfig();

        // Act
        builder.AddLogtoOIDC();
        using var host = builder.Build();

        // Assert
        var schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync("Logto");

        Assert.NotNull(scheme);
        Assert.Equal("Logto", scheme!.Name);
    }

    [Fact]
    public async Task AddLogtoOIDC_AllowsOverrideOfAuthenticationScheme()
    {
        // Arrange
        var builder = CreateBuilderWithBaseConfig();
        const string customScheme = "MyLogto";

        // Act
        builder.AddLogtoOIDC(authenticationScheme: customScheme);
        using var host = builder.Build();

        // Assert
        var schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync(customScheme);

        Assert.NotNull(scheme);
        Assert.Equal(customScheme, scheme!.Name);
    }

    [Fact]
    public void AddLogtoOIDC_UsesConnectionStringEndpoint_WhenSectionEndpointMissing()
    {
        // Arrange
        var extraConfig = new Dictionary<string, string?>
        {
            ["Aspire:Logto:Client:Endpoint"] = null,
            ["ConnectionStrings:Logto"] = "Endpoint=https://logto-from-cs.example.com"
        };

        var builder = CreateBuilderWithBaseConfig(extraConfig);

        // Act
        builder.AddLogtoOIDC(connectionName: "Logto");
        using var host = builder.Build();

        // Assert
        var optionsMonitor = host.Services.GetService<IOptionsMonitor<LogtoOptions>>();
        Assert.NotNull(optionsMonitor);

        var options = optionsMonitor!.Get("Logto");
        Assert.StartsWith("https://logto-from-cs.example.com", options.Endpoint);
        Assert.Equal("test-app-id", options.AppId);
        Assert.Equal("test-secret", options.AppSecret);
    }

    [Fact]
    public void AddLogtoClient_ConfigureSettings_CanOverrideOptions()
    {
        // Arrange
        var builder = CreateBuilderWithBaseConfig();

        // Act
        builder.AddLogtoOIDC(logtoOptions: opt =>
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

    [Fact]
    public void AddLogtoOIDC_PreservesAllLogtoOptions()
    {
        var builder = CreateBuilderWithBaseConfig();

        builder.AddLogtoOIDC(logtoOptions: options =>
        {
            options.Scopes = ["openid", "email"];
            options.Resource = "https://api.example.com";
            options.Prompt = "consent";
            options.CallbackPath = "/custom-callback";
            options.SignedOutCallbackPath = "/custom-signout";
            options.GetClaimsFromUserInfoEndpoint = true;
            options.CookieDomain = "example.com";
        });

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptionsMonitor<LogtoOptions>>().Get("Logto");

        Assert.Equal(["openid", "email"], options.Scopes);
        Assert.Equal("https://api.example.com", options.Resource);
        Assert.Equal("consent", options.Prompt);
        Assert.Equal("/custom-callback", options.CallbackPath);
        Assert.Equal("/custom-signout", options.SignedOutCallbackPath);
        Assert.True(options.GetClaimsFromUserInfoEndpoint);
        Assert.Equal("example.com", options.CookieDomain);
    }
}

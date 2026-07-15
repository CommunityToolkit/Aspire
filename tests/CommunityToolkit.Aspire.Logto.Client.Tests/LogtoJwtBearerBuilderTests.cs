using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CommunityToolkit.Aspire.Logto.Client.Tests;

public class LogtoJwtBearerBuilderTests
{
    [Fact]
    public void AddLogtoJwtBearer_ConfiguresIssuerAudiencesAndUserOptions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:logto"] = "Endpoint=https://logto.example.com/"
        });

        builder.Services.AddAuthentication()
            .AddLogtoJwtBearer(
                "logto",
                ["api-one", "api-two"],
                authenticationScheme: "LogtoBearer",
                configureOptions: options => options.RequireHttpsMetadata = false);

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("LogtoBearer");

        Assert.Equal("https://logto.example.com/oidc", options.Authority);
        Assert.Equal("https://logto.example.com/oidc", options.TokenValidationParameters.ValidIssuer);
        Assert.Equal(["api-one", "api-two"], options.TokenValidationParameters.ValidAudiences);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.False(options.RequireHttpsMetadata);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLogtoJwtBearer_RejectsEmptyAudience(string audience)
    {
        var builder = WebApplication.CreateBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.Services.AddAuthentication().AddLogtoJwtBearer("logto", audience));
    }
}

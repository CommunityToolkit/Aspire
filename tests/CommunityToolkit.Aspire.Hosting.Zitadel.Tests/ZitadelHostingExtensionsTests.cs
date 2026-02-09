using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Zitadel;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Zitadel.Tests;

public class ZitadelHostingExtensionsTests
{
    [Fact]
    public void AddZitadel_Should_Throw_If_Builder_Is_Null()
    {
        IDistributedApplicationBuilder builder = null!;

        var act = () => builder.AddZitadel("zitadel");

        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddZitadel_Should_Throw_If_Name_Is_Null()
    {
        var builder = DistributedApplication.CreateBuilder();

        var act = () => builder.AddZitadel(null!);

        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void AddZitadel_Creates_ZitadelResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var zitadel = builder.AddZitadel("zitadel");

        Assert.NotNull(zitadel);
        Assert.IsType<ZitadelResource>(zitadel.Resource);
        Assert.Equal("zitadel", zitadel.Resource.Name);
    }

    [Fact]
    public async Task AddZitadel_Sets_Default_Environment_Variables()
    {
        var builder = DistributedApplication.CreateBuilder();

        var zitadel = builder.AddZitadel("zitadel");

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("localhost", env["ZITADEL_EXTERNALDOMAIN"]);
        Assert.Equal("false", env["ZITADEL_DEFAULTINSTANCE_FEATURES_LOGINV2_REQUIRED"]);
        Assert.Equal("false", env["ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORDCHANGEREQUIRED"]);
    }

    [Fact]
    public async Task AddZitadel_Sets_Admin_Username_And_Password()
    {
        var builder = DistributedApplication.CreateBuilder();

        var zitadel = builder.AddZitadel("zitadel");

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.NotNull(zitadel.Resource.AdminUsernameParameter);
        Assert.NotNull(zitadel.Resource.AdminPasswordParameter);
        Assert.True(env.ContainsKey("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME"));
        Assert.True(env.ContainsKey("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORD"));
    }

    [Fact]
    public void AddZitadel_Uses_Custom_Username_And_Password()
    {
        var builder = DistributedApplication.CreateBuilder();
        var username = builder.AddParameter("custom-username");
        var password = builder.AddParameter("custom-password");

        var zitadel = builder.AddZitadel("zitadel", username: username, password: password);

        Assert.Same(username.Resource, zitadel.Resource.AdminUsernameParameter);
        Assert.Same(password.Resource, zitadel.Resource.AdminPasswordParameter);
    }

    [Fact]
    public async Task AddZitadel_Uses_Custom_MasterKey()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["Parameters:custom-masterkey"] = "[REDACTED]";
        var masterKey = builder.AddParameter("custom-masterkey");

        var zitadel = builder.AddZitadel("zitadel", masterKey: masterKey);

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.True(env.ContainsKey("ZITADEL_MASTERKEY"));
    }

    [Fact]
    public void AddZitadel_Has_HttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        var zitadel = builder.AddZitadel("zitadel");

        var endpoint = zitadel.Resource.Annotations.OfType<EndpointAnnotation>()
            .FirstOrDefault(e => e.Name == "http");

        Assert.NotNull(endpoint);
    }

    [Fact]
    public void AddZitadel_With_Custom_Port()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddZitadel("zitadel", port: 8888);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<ZitadelResource>());

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .First(e => e.Name == "http");

        Assert.Equal(8888, endpoint.Port);
    }

    [Fact]
    public async Task WithExternalDomain_Overrides_Default()
    {
        var builder = DistributedApplication.CreateBuilder();

        var zitadel = builder.AddZitadel("zitadel")
            .WithExternalDomain("custom.domain.com");

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("custom.domain.com", env["ZITADEL_EXTERNALDOMAIN"]);
    }

    [Fact]
    public async Task WithExternalDomain_Can_Override_Parameter()
    {
        var builder = DistributedApplication.CreateBuilder();

        var zitadel = builder.AddZitadel("zitadel")
            .WithExternalDomain("second.example.com");

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        // WithExternalDomain should override the parameter
        Assert.Equal("second.example.com", env["ZITADEL_EXTERNALDOMAIN"]);
    }

    [Fact]
    public void WithExternalDomain_Throws_If_Null()
    {
        var builder = DistributedApplication.CreateBuilder();
        var zitadel = builder.AddZitadel("zitadel");

        var act = () => zitadel.WithExternalDomain(null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void WithExternalDomain_Throws_If_Empty()
    {
        var builder = DistributedApplication.CreateBuilder();
        var zitadel = builder.AddZitadel("zitadel");

        var act = () => zitadel.WithExternalDomain("");

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void WithExternalDomain_Throws_If_Whitespace()
    {
        var builder = DistributedApplication.CreateBuilder();
        var zitadel = builder.AddZitadel("zitadel");

        var act = () => zitadel.WithExternalDomain("   ");

        Assert.Throws<ArgumentException>(act);
    }
}

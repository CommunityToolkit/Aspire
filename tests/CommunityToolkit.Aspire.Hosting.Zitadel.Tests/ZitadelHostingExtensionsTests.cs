using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Zitadel;

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

        var env = await zitadel.Resource.GetEnvironmentVariableValuesAsync();

        Assert.Equal("false", env["ZITADEL_TLS_ENABLED"]);
        Assert.Equal("false", env["ZITADEL_EXTERNALSECURE"]);
        Assert.Equal("zitadel.dev.localhost", env["ZITADEL_EXTERNALDOMAIN"]);
        Assert.Equal("false", env["ZITADEL_DEFAULTINSTANCE_FEATURES_LOGINV2_REQUIRED"]);
        Assert.Equal("false", env["ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORDCHANGEREQUIRED"]);
    }

    [Fact]
    public async Task AddZitadel_Sets_Admin_Username_And_Password()
    {
        var builder = DistributedApplication.CreateBuilder();

        var zitadel = builder.AddZitadel("zitadel");

        var env = await zitadel.Resource.GetEnvironmentVariableValuesAsync();

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
        var masterKey = builder.AddParameter("custom-masterkey");

        var zitadel = builder.AddZitadel("zitadel", masterKey: masterKey);

        var env = await zitadel.Resource.GetEnvironmentVariableValuesAsync();

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
}

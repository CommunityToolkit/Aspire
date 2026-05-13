using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Zitadel.Tests;

public class ZitadelWithDatabaseTests
{
    [Fact]
    public void WithDatabase_Should_Throw_If_Builder_Is_Null()
    {
        IResourceBuilder<ZitadelResource> builder = null!;
        var app = DistributedApplication.CreateBuilder();
        var pg = app.AddPostgres("postgres");

        var act = () => builder.WithDatabase(pg);

        var exception = Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void WithDatabase_Should_Throw_If_Server_Is_Null()
    {
        var app = DistributedApplication.CreateBuilder();
        var zitadel = app.AddZitadel("zitadel");

        var act = () => zitadel.WithDatabase((IResourceBuilder<PostgresServerResource>)null!);

        var exception = Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void WithDatabase_Should_Throw_If_Database_Is_Null()
    {
        var app = DistributedApplication.CreateBuilder();
        var zitadel = app.AddZitadel("zitadel");

        var act = () => zitadel.WithDatabase((IResourceBuilder<PostgresDatabaseResource>)null!);

        var exception = Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public async Task WithDatabase_Sets_Default_Database_Name()
    {
        var builder = DistributedApplication.CreateBuilder();
        var pg = builder.AddPostgres("postgres");
        var zitadel = builder.AddZitadel("zitadel")
            .WithDatabase(pg);

        UpdateResourceEndpoint(pg.Resource);

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("zitadel-db", env["ZITADEL_DATABASE_POSTGRES_DATABASE"]);
    }

    [Fact]
    public async Task WithDatabase_Uses_Custom_Database_Name()
    {
        var builder = DistributedApplication.CreateBuilder();
        var pg = builder.AddPostgres("postgres");
        var zitadel = builder.AddZitadel("zitadel")
            .WithDatabase(pg, "custom-db");

        UpdateResourceEndpoint(pg.Resource);

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("custom-db", env["ZITADEL_DATABASE_POSTGRES_DATABASE"]);
    }

    [Fact]
    public async Task WithDatabase_Sets_Postgres_Environment_Variables()
    {
        var builder = DistributedApplication.CreateBuilder();
        var pg = builder.AddPostgres("postgres");
        var db = pg.AddDatabase("zitadel-db");

        var zitadel = builder.AddZitadel("zitadel")
            .WithDatabase(db);

        UpdateResourceEndpoint(pg.Resource);

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_USER_USERNAME"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_USER_PASSWORD"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_HOST"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_PORT"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_DATABASE"));
    }

    [Fact]
    public async Task WithDatabase_Uses_Server_Parameters()
    {
        var builder = DistributedApplication.CreateBuilder();
        var pg = builder.AddPostgres("postgres");
        var db = pg.AddDatabase("zitadel-db");

        var zitadel = builder.AddZitadel("zitadel")
            .WithDatabase(db);

        UpdateResourceEndpoint(pg.Resource);

        var env = await zitadel.Resource.GetEnvironmentVariablesAsync();

        Assert.NotNull(env["ZITADEL_DATABASE_POSTGRES_USER_USERNAME"]);
        Assert.NotNull(env["ZITADEL_DATABASE_POSTGRES_USER_PASSWORD"]);
        Assert.NotNull(env["ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME"]);
        Assert.NotNull(env["ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD"]);
    }

    [Fact]
    public void WithDatabase_Creates_WaitFor_Dependency()
    {
        var app = DistributedApplication.CreateBuilder();
        var pg = app.AddPostgres("postgres");
        var db = pg.AddDatabase("zitadel-db");

        var zitadel = app.AddZitadel("zitadel")
            .WithDatabase(db);

        // The resource should have a WaitFor annotation
        var waitForAnnotation = zitadel.Resource.Annotations
            .OfType<WaitAnnotation>()
            .FirstOrDefault();

        Assert.NotNull(waitForAnnotation);
    }

    [Fact]
    public void WithDatabase_Creates_Reference()
    {
        var app = DistributedApplication.CreateBuilder();
        var pg = app.AddPostgres("postgres");
        var db = pg.AddDatabase("zitadel-db");

        var zitadel = app.AddZitadel("zitadel")
            .WithDatabase(db);

        // The resource should have a ResourceReference annotation
        var references = zitadel.Resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .ToList();
        
        Assert.NotEmpty(references);
    }

    static void UpdateResourceEndpoint(IResourceWithEndpoints resource)
    {
        var endpoint = resource.GetEndpoint("tcp").EndpointAnnotation;
        var ae = new AllocatedEndpoint(endpoint, "storage.dev.internal", 10000, EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(KnownNetworkIdentifiers.DefaultAspireContainerNetwork, ae);
    }
}

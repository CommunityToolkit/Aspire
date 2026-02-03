using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Permify.Tests;

public class PermifyHostingExtensionTests
{
    [Fact]
    public void AddPermify_Should_Throw_If_Builder_Is_Null()
    {
        IDistributedApplicationBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddPermify("permify"));
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void Add_Permify_Should_Throw_If_Name_Is_Null()
    {
        var builder = DistributedApplication.CreateBuilder();

        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddPermify(null!));
        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void AddPermify_Should_Add_Permify_Resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var permify = builder.AddPermify("permify");

        Assert.NotNull(permify);
        Assert.IsType<PermifyResource>(permify.Resource);
        Assert.Equal("permify", permify.Resource.Name);
    }

    [Fact]
    public async Task AddPermify_Should_Add_HTTP_Endpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        var permify = builder.AddPermify("permify");

        Assert.NotNull(await permify.GetEndpoint("https").GetValueAsync());
    }

    [Fact]
    public async Task AddPermify_Should_Enable_HTTP()
    {
        var builder = DistributedApplication.CreateBuilder();
        var permify = builder.AddPermify("permify");

        var environment = await permify.Resource.GetEnvironmentVariablesAsync();
        Assert.Equal("true", environment["PERMIFY_HTTP_ENABLED"]);
    }

    [Fact]
    public async Task AddPermify_Should_Configure_SSL()
    {
        var builder = DistributedApplication.CreateBuilder();
        var permify = builder.AddPermify("permify");

        var environment = await permify.Resource.GetEnvironmentVariablesAsync();
        Assert.Equal("true", environment["PERMIFY_HTTP_TLS_ENABLED"]);
        Assert.Equal("true", environment["PERMIFY_GRPC_TLS_ENABLED"]);
        Assert.NotEmpty(environment["PERMIFY_HTTP_TLS_CERT_PATH"]);
        Assert.NotEmpty(environment["PERMIFY_HTTP_TLS_KEY_PATH"]);
        Assert.NotEmpty(environment["PERMIFY_GRPC_TLS_CERT_PATH"]);
        Assert.NotEmpty(environment["PERMIFY_GRPC_TLS_KEY_PATH"]);
    }

    [Fact]
    public async Task AddPermify_WithGrpc_Should_Enable_GRPC()
    {
        var builder = DistributedApplication.CreateBuilder();
        var permify = builder.AddPermify("permify")
            .WithGrpc();

        var environment = await permify.Resource.GetEnvironmentVariablesAsync();
        Assert.Equal("true", environment["PERMIFY_GRPC_ENABLED"]);
        Assert.NotNull(await permify.GetEndpoint("grpc").GetValueAsync());
    }

    [Fact]
    public async Task AddPermify_WithDatabase_Should_Configure_Database()
    {
        var builder = DistributedApplication.CreateBuilder();
        var database = builder.AddPostgres("postgres")
            .AddDatabase("permify-db");

        var permify = builder.AddPermify("permify")
            .WithDatabase(database);

        var environment = await permify.Resource.GetEnvironmentVariablesAsync();
        Assert.Equal("postgres", environment["PERMIFY_DATABASE_ENGINE"]);
        Assert.NotNull(environment["PERMIFY_DATABASE_URI"]);
    }

    [Fact]
    public async Task AddPermify_WithWatchSupport_Should_Configure_Watch()
    {
        var builder = DistributedApplication.CreateBuilder();
        var database = builder.AddPostgres("postgres")
            .AddDatabase("permify-db");

        var permify = builder.AddPermify("permify")
            .WithWatchSupport(database);

        var environment = await permify.Resource.GetEnvironmentVariablesAsync();
        Assert.Equal("postgres", environment["PERMIFY_DATABASE_ENGINE"]);
        Assert.NotNull(environment["PERMIFY_DATABASE_URI"]);
        Assert.Equal("true", environment["PERMIFY_SERVICE_WATCH_ENABLED"]);
    }
}
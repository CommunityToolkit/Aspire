using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using System.Net.Sockets;

namespace CommunityToolkit.Aspire.Hosting.Listmonk.Tests;

public class AddListmonkTests
{
    [Fact]
    public async Task AddListmonkContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var listmonk = appBuilder.AddListmonk("listmonk");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ListmonkResource>());
        Assert.Equal("listmonk", containerResource.Name);
        Assert.DoesNotContain(appModel.Resources, resource => resource is PostgresServerResource or PostgresDatabaseResource);

        var endpoint = Assert.Single(containerResource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(9000, endpoint.TargetPort);
        Assert.False(endpoint.IsExternal);
        Assert.Equal("http", endpoint.Name);
        Assert.Null(endpoint.Port);
        Assert.Equal(ProtocolType.Tcp, endpoint.Protocol);
        Assert.Equal("http", endpoint.Transport);
        Assert.Equal("http", endpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(ListmonkContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(ListmonkContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(ListmonkContainerImageTags.Registry, containerAnnotation.Registry);

        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource);

        Assert.Equal("192.0.2.1:9000", config["LISTMONK_app__address"]);
    }

    [Fact]
    public async Task WithReferenceConfiguresPostgresDatabase()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var postgres = appBuilder.AddPostgres("mailing-postgres");
        AllocateContainerEndpoint(postgres.Resource.PrimaryEndpoint.EndpointAnnotation, "mailing-postgres.dev.internal", 15432);

        var database = postgres.AddDatabase("listmonkdb");
        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithReference(database);
        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource);

        Assert.Equal("mailing-postgres.dev.internal", config["LISTMONK_db__host"]);
        Assert.Equal("5432", config["LISTMONK_db__port"]);
        Assert.Equal("postgres", config["LISTMONK_db__user"]);
        Assert.Equal("listmonkdb", config["LISTMONK_db__database"]);
        Assert.Equal("disable", config["LISTMONK_db__ssl_mode"]);

        Assert.Contains(listmonk.Resource.Annotations.OfType<WaitAnnotation>(), annotation => annotation.Resource == database.Resource);
    }

    [Fact]
    public async Task WithAppAddressAddsAppAddressConfiguration()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithAppAddress("192.0.2.1");

        Assert.Equal("192.0.2.1", listmonk.Resource.PrimaryEndpoint.EndpointAnnotation.TargetHost);

        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource, "192.0.2.1");

        Assert.Equal("192.0.2.1:9000", config["LISTMONK_app__address"]);
    }

    [Fact]
    public async Task WithDatabaseConfigurationMethodsAddEnvironmentVariables()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var postgres = appBuilder.AddPostgres("postgres");
        AllocateContainerEndpoint(postgres.Resource.PrimaryEndpoint.EndpointAnnotation, "postgres.dev.internal", 15432);

        var database = postgres.AddDatabase("listmonkdb");
        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithReference(database)
            .WithDatabaseSslMode("require")
            .WithDatabaseMaxOpenConnections(50)
            .WithDatabaseMaxIdleConnections(10)
            .WithDatabaseMaxLifetime("600s")
            .WithDatabaseParameters("application_name=listmonk gssencmode=disable");
        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource);

        Assert.Equal("require", config["LISTMONK_db__ssl_mode"]);
        Assert.Equal("50", config["LISTMONK_db__max_open"]);
        Assert.Equal("10", config["LISTMONK_db__max_idle"]);
        Assert.Equal("600s", config["LISTMONK_db__max_lifetime"]);
        Assert.Equal("application_name=listmonk gssencmode=disable", config["LISTMONK_db__params"]);
    }

    [Fact]
    public async Task WithTimeZoneAddsTimeZoneEnvironmentVariable()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithTimeZone("Europe/Kiev");
        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource);

        Assert.Equal("Europe/Kiev", config["TZ"]);
    }

    [Fact]
    public async Task WithUserIdAndWithGroupIdAddContainerUserEnvironmentVariables()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithUserId(1000)
            .WithGroupId(1001);
        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource);

        Assert.Equal("1000", config["PUID"]);
        Assert.Equal("1001", config["PGID"]);
    }

    [Fact]
    public async Task WithAdminCredentialsAddsFirstRunAdminEnvironmentVariables()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var adminPassword = appBuilder.AddParameter("admin-password", "SuperSecret123!", secret: true);
        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithAdminCredentials("admin", adminPassword);
        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource);

        Assert.Equal("admin", config["LISTMONK_ADMIN_USER"]);
        Assert.Equal("SuperSecret123!", config["LISTMONK_ADMIN_PASSWORD"]);
    }

    [Fact]
    public async Task WithAdminUserAndPasswordAddFirstRunAdminEnvironmentVariables()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var adminPassword = appBuilder.AddParameter("admin-password", "SuperSecret123!", secret: true);
        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithAdminUser("admin")
            .WithAdminPassword(adminPassword);
        var config = await EvaluateEnvironmentVariablesAsync(listmonk.Resource);

        Assert.Equal("admin", config["LISTMONK_ADMIN_USER"]);
        Assert.Equal("SuperSecret123!", config["LISTMONK_ADMIN_PASSWORD"]);
    }

    private static ValueTask<Dictionary<string, string>> EvaluateEnvironmentVariablesAsync(
        ListmonkResource resource,
        string host = "192.0.2.1")
    {
        resource.PrimaryEndpoint.EndpointAnnotation.AllocatedEndpoint =
            new AllocatedEndpoint(
                resource.PrimaryEndpoint.EndpointAnnotation,
                "localhost",
                19000);
        resource.PrimaryEndpoint.EndpointAnnotation.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
            KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
            new AllocatedEndpoint(
                resource.PrimaryEndpoint.EndpointAnnotation,
                host,
                resource.PrimaryEndpoint.EndpointAnnotation.TargetPort!.Value,
                EndpointBindingMode.SingleAddress,
                networkId: KnownNetworkIdentifiers.DefaultAspireContainerNetwork));

        return resource.GetEnvironmentVariablesAsync();
    }

    private static void AllocateContainerEndpoint(EndpointAnnotation endpoint, string containerHost, int hostPort)
    {
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "localhost", hostPort);
        endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
            KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
            new AllocatedEndpoint(
                endpoint,
                containerHost,
                endpoint.TargetPort!.Value,
                EndpointBindingMode.SingleAddress,
                networkId: KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
    }

    [Fact]
    public void WithUploadsVolumeAddsVolumeAnnotation()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithUploadsVolume("uploads");

        var volume = Assert.Single(listmonk.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal("uploads", volume.Source);
        Assert.Equal("/listmonk/uploads", volume.Target);
        Assert.Equal(ContainerMountType.Volume, volume.Type);
    }

    [Fact]
    public void WithUploadsBindMountAddsBindMountAnnotation()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var listmonk = appBuilder.AddListmonk("listmonk")
            .WithUploadsBindMount("./uploads");

        var volume = Assert.Single(listmonk.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(Path.Combine(appBuilder.AppHostDirectory, "uploads"), volume.Source);
        Assert.Equal("/listmonk/uploads", volume.Target);
        Assert.Equal(ContainerMountType.BindMount, volume.Type);
    }
}

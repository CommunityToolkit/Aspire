using Aspire.Components.Common.Tests;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Keycloak.Extensions.Tests;

[RequiresDocker]
public class KeycloakWithPostgresIntegrationTest
{
    [Fact]
    public async Task Keycloak_WithPostgres_Starts_And_HealthReady_Ok()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var pg = builder.AddPostgres("pg");
        var db = pg.AddDatabase("keycloakdb");

        var kc = builder.AddKeycloak("kc")
            .WithPostgres(db);

        await using var app = await builder.BuildAsync();

        await app.StartAsync();

        await app.ResourceNotifications.WaitForResourceAsync(pg.Resource.Name, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));
        await app.ResourceNotifications.WaitForResourceAsync(kc.Resource.Name, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(5));

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync(pg.Resource.Name)
            .WaitAsync(TimeSpan.FromMinutes(3));


        await app.ResourceNotifications
            .WaitForResourceHealthyAsync(kc.Resource.Name)
            .WaitAsync(TimeSpan.FromMinutes(5));


        using var http = app.CreateHttpClient(kc.Resource.Name, "management");
        var response = await http.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await app.StopAsync();
    }

    [Fact]
    public async Task Keycloak_WithPostgres_Env_Is_Applied_And_DbUrl_Is_Valid()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var pg = builder.AddPostgres("pg");
        var db = pg.AddDatabase("keycloakdb");

        var kc = builder.AddKeycloak("kc")
            .WithPostgres(db);

        await using var app = await builder.BuildAsync();
        await app.StartAsync();

        await app.ResourceNotifications.WaitForResourceAsync(pg.Resource.Name, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));
        await app.ResourceNotifications.WaitForResourceAsync(kc.Resource.Name, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(5));

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync(pg.Resource.Name)
            .WaitAsync(TimeSpan.FromMinutes(3));


        await app.ResourceNotifications
            .WaitForResourceHealthyAsync(kc.Resource.Name)
            .WaitAsync(TimeSpan.FromMinutes(5));

        Assert.True(kc.Resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var annotations));

        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        var env = context.EnvironmentVariables;

        Assert.Equal("postgres", env["KC_DB"]);
        Assert.True(env.TryGetValue("KC_DB_URL", out var v));
        var exp = Assert.IsType<ReferenceExpression>(v);
        Assert.StartsWith("jdbc:postgresql://", exp.Format);
        Assert.EndsWith("/keycloakdb", exp.Format);

        await app.StopAsync();
    }
}
using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Zitadel.Tests;

[RequiresDocker]
public class ZitadelIntegrationTests(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Zitadel_AppHost> fixture
) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Zitadel_AppHost>>
{
    [Fact]
    public async Task Zitadel_WithPostgres_Starts_And_HealthReady_Ok()
    {
        var postgresName = "postgres";
        var zitadelName = "zitadel";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(postgresName)
            .WaitAsync(TimeSpan.FromMinutes(3));

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(zitadelName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(zitadelName);
        var response = await httpClient.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Zitadel_WithPostgres_Env_Is_Applied_And_DbConfig_Is_Valid()
    {
        var postgresName = "postgres";
        var zitadelName = "zitadel";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(postgresName)
            .WaitAsync(TimeSpan.FromMinutes(3));

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(zitadelName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var zitadelResource = appModel.Resources.OfType<ZitadelResource>()
            .Single(r => r.Name == zitadelName);

        var env = await zitadelResource.GetEnvironmentVariableValuesAsync();

        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_HOST"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_PORT"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_DATABASE"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_USER_USERNAME"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_USER_PASSWORD"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME"));
        Assert.True(env.ContainsKey("ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD"));
    }

    [Fact]
    public async Task Zitadel_Admin_Credentials_Are_Set()
    {
        var zitadelName = "zitadel";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(zitadelName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var zitadelResource = appModel.Resources.OfType<ZitadelResource>()
            .Single(r => r.Name == zitadelName);

        var env = await zitadelResource.GetEnvironmentVariableValuesAsync();

        Assert.True(env.ContainsKey("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME"));
        Assert.True(env.ContainsKey("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORD"));
        Assert.NotEmpty(env["ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME"]);
        Assert.NotEmpty(env["ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORD"]);
    }
}

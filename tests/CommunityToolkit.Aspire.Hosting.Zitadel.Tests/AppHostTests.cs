using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Zitadel.Tests;

[RequiresDocker]
public class AppHostTests(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Zitadel_AppHost> fixture
) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Zitadel_AppHost>>
{
    [Fact]
    public async Task Zitadel_Starts_And_Responds_Ok()
    {
        var resourceName = "zitadel";

        // Wait for Zitadel to be healthy (it has a health check configured)
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);

        // Test the health endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        // Needs to match the external domain for Zitadel or we get a 404
        request.Headers.Host = $"{resourceName}.dev.localhost";
        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Zitadel_Starts_And_Serves_Dashboard()
    {
        var resourceName = "zitadel";

        // Wait for Zitadel to be healthy (it has a health check configured)
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);

        // Test the health endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        // Needs to match the external domain for Zitadel or we get a 404
        request.Headers.Host = $"{resourceName}.dev.localhost";
        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("<html", body);
        Assert.Contains("cnsl-root", body);
    }

    [Fact]
    public async Task Zitadel_With_Postgres_Starts_And_Is_Healthy()
    {
        var postgresName = "postgres";
        var zitadelName = "zitadel";

        // Wait for Postgres to be healthy first
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(postgresName)
            .WaitAsync(TimeSpan.FromMinutes(3));

        // Then wait for Zitadel to be healthy
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(zitadelName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(zitadelName);

        // Test the health endpoint
        var healthResponse = await httpClient.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
    }

    [Fact]
    public async Task Zitadel_Dashboard_Is_Accessible()
    {
        var resourceName = "zitadel";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);

        // The dashboard should be accessible at the root
        var response = await httpClient.GetAsync("/healthz");

        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }
}

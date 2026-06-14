using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RedPanda.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_RedPanda_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_RedPanda_AppHost>>
{
    private const string ResourceName = "redpanda";

    [Fact]
    public async Task ResourceStartsAndAdminApiReportsReady()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(ResourceName).WaitAsync(TimeSpan.FromMinutes(2));

        HttpClient client = fixture.CreateHttpClient(ResourceName, "admin");

        HttpResponseMessage response = await client.GetAsync("/v1/status/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SchemaRegistryRespondsOk()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(ResourceName).WaitAsync(TimeSpan.FromMinutes(2));

        HttpClient client = fixture.CreateHttpClient(ResourceName, "schemaregistry");

        HttpResponseMessage response = await client.GetAsync("/subjects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

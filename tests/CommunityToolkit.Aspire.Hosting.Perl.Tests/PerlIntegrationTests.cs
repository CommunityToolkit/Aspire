using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlIntegrationTests(AspireIntegrationTestFixture<Projects.CpanmApiIntegration_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CpanmApiIntegration_AppHost>>
{
    [Fact, RequiresLinux]
    public async Task CpanmApiIntegration_FleetingEndpoint_ReturnsExpectedText()
    {
        const string resourceName = "perl-api";
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(2));

        var response = await httpClient.GetAsync("/fleeting");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("fragile", body);
    }
}

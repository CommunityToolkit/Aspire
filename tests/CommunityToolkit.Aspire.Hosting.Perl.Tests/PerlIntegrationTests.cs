using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlIntegrationTests(AspireIntegrationTestFixture<Projects.CpanmApiIntegration_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CpanmApiIntegration_AppHost>>
{
    #region Test Helpers

    private static async Task WaitForExpectedTextAsync(
        HttpClient httpClient,
        string path,
        string expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync(path);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (body == expected)
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"Endpoint '{path}' did not return expected response in time. Last exception: {lastException?.Message}");
    }

    #endregion

    #region Cpanm API Integration Endpoints


    [Fact]
    public async Task CpanmApiIntegration_FleetingEndpoint_ReturnsExpectedText()
    {
        const string resourceName = "perl-api";
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.ResourceNotificationService
            .WaitForResourceAsync(resourceName, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(2));

        const string expected = "fragile";
        await WaitForExpectedTextAsync(httpClient, "/fleeting", expected, TimeSpan.FromMinutes(2));
    }

    #endregion

}

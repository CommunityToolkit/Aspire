using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlIntegrationTests(AspireIntegrationTestFixture<Projects.MultiResource_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.MultiResource_AppHost>>
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

    #region Multi-resource API Endpoints


    [Fact]
    public async Task MultiResourceApi_PachinkoEndpoint_ReturnsExpectedText()
    {
        const string resourceName = "perl-api";
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.ResourceNotificationService
            .WaitForResourceAsync(resourceName, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(2));

        const string expected = "My name was Pachinko, I sang the Flamenco - my songs are something you never will forget!";
        await WaitForExpectedTextAsync(httpClient, "/pachinko", expected, TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task MultiResourceApi_OmegaEndpoint_ReturnsSecondLayerText()
    {
        const string resourceName = "perl-api";
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.ResourceNotificationService
            .WaitForResourceAsync(resourceName, KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(2));

        const string expected = "Sometimes there's a crack in the world that I can peer through to see it for what it really is.";
        await WaitForExpectedTextAsync(httpClient, "/omega", expected, TimeSpan.FromMinutes(2));
    }

    #endregion

}

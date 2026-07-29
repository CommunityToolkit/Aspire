using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Squad_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Squad_AppHost>>
{
    [Fact]
    public async Task SquadSampleAppHost_StartsAndServesMetadata()
    {
        const string resourceName = "squad-api";
        var httpClient = fixture.CreateHttpClient(resourceName);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        HttpResponseMessage? response = null;
        while (!cts.IsCancellationRequested)
        {
            try
            {
                response = await httpClient.GetAsync("/", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch when (!cts.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
        }

        Assert.NotNull(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("research-squad", body, StringComparison.Ordinal);
        Assert.Contains("dev-squad", body, StringComparison.Ordinal);
        Assert.Contains("/ask", body, StringComparison.Ordinal);
    }
}

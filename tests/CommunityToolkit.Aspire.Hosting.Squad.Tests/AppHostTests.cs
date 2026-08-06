using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Squad_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Squad_AppHost>>
{
    [Fact]
    public async Task SquadSampleAppHost_StartsAndServesMetadata()
    {
        const string resourceName = "squad-api";
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var model = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var apiApp = model.Resources
            .OfType<ProjectResource>()
            .Single(resource => resource.Name == resourceName);

        using HttpClient httpClient = new()
        {
            BaseAddress = new Uri(apiApp.GetEndpoint("http").Url)
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(2));

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

                response.Dispose();
                response = null;
            }
            catch (HttpRequestException) when (!cts.IsCancellationRequested)
            {
            }
            catch (TaskCanceledException) when (!cts.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }

        Assert.NotNull(response);
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("research-squad", body, StringComparison.Ordinal);
            Assert.Contains("dev-squad", body, StringComparison.Ordinal);
            Assert.Contains("/ask", body, StringComparison.Ordinal);
        }
    }
}

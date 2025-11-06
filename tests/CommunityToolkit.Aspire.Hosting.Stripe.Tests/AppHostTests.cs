using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Stripe.Tests;

public class AppHostTests(AspireIntegrationTestFixture<AppHost.AppHostMarker> fixture) 
    : IClassFixture<AspireIntegrationTestFixture<AppHost.AppHostMarker>>
{
    [Fact]
    [Trait("RequiresTools", "stripe")]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "api";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(1));

        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Stripe Webhook API", body);
    }

    [Fact]
    [Trait("RequiresTools", "stripe")]
    public async Task StripeResourceIsCreated()
    {
        var app = fixture.App;
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var stripeResource = appModel.Resources.OfType<StripeResource>().SingleOrDefault();
        Assert.NotNull(stripeResource);
        Assert.Equal("stripe", stripeResource.Name);
    }
}

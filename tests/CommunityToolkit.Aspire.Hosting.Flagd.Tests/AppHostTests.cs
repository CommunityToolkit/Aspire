using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using OpenFeature.Contrib.Providers.Flagd;
using OpenFeature.Providers.Ofrep;
using OpenFeature.Providers.Ofrep.Configuration;

namespace CommunityToolkit.Aspire.Hosting.Flagd.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Flagd_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Flagd_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsCorrectlyForFlagdEvaluation()
    {
        var resourceName = "flagd";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));

        var connectionString = await fixture.GetConnectionString(resourceName);
        Assert.NotNull(connectionString);

        await OpenFeature.Api.Instance.SetProviderAsync(new FlagdProvider(new Uri(connectionString)));

        var flagClient = OpenFeature.Api.Instance.GetClient();
        var welcomeBanner = await flagClient.GetBooleanDetailsAsync("welcome-banner", false);
        var backgroundColor = await flagClient.GetStringDetailsAsync("background-color", "000000");
        var apiVersion = await flagClient.GetStringDetailsAsync("api-version", "0.1");

        Assert.True(welcomeBanner.Value);
        Assert.Equal("#FF0000", backgroundColor.Value);
        Assert.Equal("1.0", apiVersion.Value);
    }

    [Fact]
    public async Task ResourceStartsAndRespondsCorrectlyForOfrepEvaluation()
    {
        var resourceName = "flagd";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));

        var connectionString = fixture.GetEndpoint(resourceName, "ofrep");
        Assert.NotNull(connectionString);

        // Configure the provider
        var config = new OfrepOptions(connectionString.ToString());

        await OpenFeature.Api.Instance.SetProviderAsync(new OfrepProvider(config));

        var flagClient = OpenFeature.Api.Instance.GetClient();
        var welcomeBanner = await flagClient.GetBooleanDetailsAsync("welcome-banner", false);
        var backgroundColor = await flagClient.GetStringDetailsAsync("background-color", "000000");
        var apiVersion = await flagClient.GetStringDetailsAsync("api-version", "0.1");

        Assert.True(welcomeBanner.Value);
        Assert.Equal("#FF0000", backgroundColor.Value);
        Assert.Equal("1.0", apiVersion.Value);
    }
}

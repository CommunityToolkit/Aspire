using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using System.Net;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Floci_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Floci_AppHost>>
{
    private const string AwsResourceName = "floci-aws";
    private const string AzureResourceName = "floci-az";
    private const string GcpResourceName = "floci-gcp";
    private const string UIResourceName = "floci-ui";

    [Theory]
    [InlineData(AwsResourceName, "aws", "/_floci/info")]
    [InlineData(AzureResourceName, "azure", "/_floci/health")]
    [InlineData(GcpResourceName, "gcp", "/_floci-gcp/health")]
    [InlineData(UIResourceName, "http", "/")]
    public async Task ResourceStartsAndRespondsOk(string resourceName, string endpointName, string path)
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        HttpClient httpClient = fixture.CreateHttpClient(resourceName, endpointName);

        HttpResponseMessage response = await httpClient.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiServiceReachesAllThreeCloudsThroughInjectedEnvironmentVariables()
    {
        // The ApiService registers one health check per cloud (floci-s3, floci-azure-blob,
        // floci-gcp-storage), each of which performs a real SDK call against the emulator using
        // only the environment variables WithReference injected. A healthy /health therefore
        // asserts the whole reference-injection path end to end.
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync("floci-api", TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        HttpClient httpClient = fixture.CreateHttpClient("floci-api");

        HttpResponseMessage response = await httpClient.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

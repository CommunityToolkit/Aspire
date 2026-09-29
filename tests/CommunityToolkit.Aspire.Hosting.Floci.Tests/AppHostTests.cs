using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

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
    // floci-aws serves HTTPS using the certificate Aspire provisioned for it, on the same port.
    // Reaching it over https proves the whole chain: Aspire's certificate callback fired, the paths
    // were mapped onto FLOCI_TLS_CERT_PATH/FLOCI_TLS_KEY_PATH, and the endpoint was re-scheme'd.
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

    [Theory]
    [InlineData("aws")]
    [InlineData("azure")]
    [InlineData("gcp")]
    public async Task FlociUIReachesEachAttachedCloud(string cloud)
    {
        // The console's own API runs the per-cloud runtime probe against FLOCI_ENDPOINT /
        // FLOCI_AZURE_ENDPOINT / FLOCI_GCP_ENDPOINT, so this is the assertion that the UI can
        // actually talk to the emulators — the resource merely being healthy only proves it serves
        // its SPA. floci-aws is HTTPS in this AppHost, which is what makes this a regression test:
        // the UI reaches the emulator by container-network name, which no host certificate covers,
        // so it must keep using the plain-HTTP listener on the same port.
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(UIResourceName, TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        HttpClient httpClient = fixture.CreateHttpClient(UIResourceName, "http");

        var status = await httpClient.GetFromJsonAsync<CloudStatus>(
            $"/api/clouds/{cloud}/status", TestContext.Current.CancellationToken);

        Assert.NotNull(status);
        Assert.Equal("reachable", status.Runtime);
        Assert.Null(status.Error);
        Assert.StartsWith("http://", status.Endpoint);
    }

    private sealed record CloudStatus(
        [property: JsonPropertyName("runtime")] string Runtime,
        [property: JsonPropertyName("endpoint")] string? Endpoint,
        [property: JsonPropertyName("error")] string? Error);

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

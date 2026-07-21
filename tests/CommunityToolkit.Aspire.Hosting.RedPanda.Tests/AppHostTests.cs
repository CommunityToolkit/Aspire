using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.RedPanda.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_RedPanda_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_RedPanda_AppHost>>
{
    private const string ResourceName = "redpanda";
    private const string ConsoleResourceName = "redpanda-console";
    private const string KafkaUiResourceName = "redpanda-kafka-ui";
    private const string ConsumerResourceName = "consumer";

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

    [Fact]
    public async Task ConsumerConnectsToBrokerAndBecomesHealthy()
    {
        // The consumer's Aspire Kafka producer/consumer health checks are surfaced to Aspire via
        // WithHttpHealthCheck("/health"), so reaching a healthy state proves it connected to the broker.
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(ConsumerResourceName).WaitAsync(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task RedPandaConsoleUiStartsAndServesRequests()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(ConsoleResourceName).WaitAsync(TimeSpan.FromMinutes(2));

        HttpClient client = fixture.CreateHttpClient(ConsoleResourceName, "http");

        HttpResponseMessage response = await GetWithRetryAsync(client, "/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task KafkaUiStartsAndServesRequests()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(KafkaUiResourceName).WaitAsync(TimeSpan.FromMinutes(2));

        HttpClient client = fixture.CreateHttpClient(KafkaUiResourceName, "http");

        HttpResponseMessage response = await GetWithRetryAsync(client, "/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The UI containers report healthy as soon as they are running, but their web servers may take a
    // moment longer to start serving requests, so poll the endpoint until it responds successfully.
    private static async Task<HttpResponseMessage> GetWithRetryAsync(HttpClient client, string requestUri)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));

        while (true)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(requestUri, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }
            }
            catch (HttpRequestException) when (!cts.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        }
    }
}

using Aspire.Hosting;
using Aspire.Hosting.Eventing;

namespace CommunityToolkit.Aspire.Hosting.Ngrok.Tests;

public class NgrokConfigurationFileTests
{
    [Fact]
    public async Task Version2ConfigurationWritesProtoAndAddrForTunnelWithoutLabels()
    {
        var configuration = await WriteConfigurationAsync(2, labels: null);

        Assert.Contains("  api-http:", configuration);
        Assert.Contains("    proto: http", configuration);
        Assert.Contains("    addr: http://CONTAINER-HOSTNAME:8080", configuration);
    }

    [Fact]
    public async Task Version2ConfigurationWritesLabelsAndAddrForLabeledTunnel()
    {
        var configuration = await WriteConfigurationAsync(2, new Dictionary<string, string> { ["edge"] = "edghts_123" });

        Assert.Contains("      - edge=edghts_123", configuration);
        Assert.Contains("    addr: http://CONTAINER-HOSTNAME:8080", configuration);
        Assert.DoesNotContain("proto:", configuration);
    }

    private static async Task<string> WriteConfigurationAsync(int configurationVersion, Dictionary<string, string>? labels)
    {
        var configurationFolder = Directory.CreateTempSubdirectory("ngrok-tests-").FullName;
        try
        {
            var builder = DistributedApplication.CreateBuilder();

            var api = builder.AddContainer("api", "image")
                .WithHttpEndpoint(targetPort: 8080, name: "http");

            builder.AddNgrok("ngrok", configurationFolder, configurationVersion: configurationVersion)
                .WithTunnelEndpoint(api, "http", labels: labels);

            using var app = builder.Build();

            var endpointAnnotation = api.Resource.Annotations.OfType<EndpointAnnotation>().Single();
            endpointAnnotation.AllocatedEndpoint = new AllocatedEndpoint(endpointAnnotation, "LOCALHOST-HOSTNAME", 18080);
            endpointAnnotation.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
                KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
                new AllocatedEndpoint(
                    endpointAnnotation,
                    "CONTAINER-HOSTNAME",
                    8080,
                    EndpointBindingMode.SingleAddress,
                    networkId: KnownNetworkIdentifiers.DefaultAspireContainerNetwork));

            var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
            var resource = Assert.Single(appModel.Resources.OfType<NgrokResource>());

            var eventing = app.Services.GetRequiredService<IDistributedApplicationEventing>();
            await eventing.PublishAsync(new BeforeResourceStartedEvent(resource, app.Services));

            return await File.ReadAllTextAsync(Path.Combine(configurationFolder, "ngrok.yml"));
        }
        finally
        {
            Directory.Delete(configurationFolder, recursive: true);
        }
    }
}

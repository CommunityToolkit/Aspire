using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class MultiCloudUITests
{
    [Fact]
    public void WithReferenceAttachesAdditionalCloudsToSingleUIContainer()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        var gcp = builder.AddFlociGcp("floci-gcp");

        builder.AddFlociAws("floci")
            .WithFlociUI(configureContainer: ui =>
            {
                ui.WithReference(azure);
                ui.WithReference(gcp);
            });

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Only one UI container is created, even though three clouds are attached to it.
        Assert.Single(appModel.Resources.OfType<FlociUIContainerResource>());
    }

    [Fact]
    public async Task WithReferenceSetsEachCloudsEnvironmentVariablesOnTheSharedUIContainer()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        var gcp = builder.AddFlociGcp("floci-gcp", defaultProjectId: "my-project");

        builder.AddFlociAws("floci")
            .WithFlociUI(configureContainer: ui =>
            {
                ui.WithReference(azure);
                ui.WithReference(gcp);
            });

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var uiResource = appModel.Resources.OfType<FlociUIContainerResource>().Single();

        Assert.True(uiResource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        // AWS vars — set because the UI was created via floci.WithFlociUI().
        Assert.True(envVars.ContainsKey(FlociUIContainerResource.EndpointEnvVar));
        Assert.Equal("test", envVars[FlociUIContainerResource.AccessKeyIdEnvVar].ToString());

        // Azure and GCP vars — set via WithReference on the same container.
        var azureEndpoint = Assert.IsType<ReferenceExpression>(envVars[FlociUIContainerResource.AzureEndpointEnvVar]);
        Assert.Equal("http://{floci-az.bindings.azure.host}:{floci-az.bindings.azure.port}", azureEndpoint.ValueExpression);
        Assert.Equal("devstoreaccount1", envVars[FlociUIContainerResource.AzureAccountNameEnvVar].ToString());

        var gcpEndpoint = Assert.IsType<ReferenceExpression>(envVars[FlociUIContainerResource.GcpEndpointEnvVar]);
        Assert.Equal("http://{floci-gcp.bindings.gcp.host}:{floci-gcp.bindings.gcp.port}", gcpEndpoint.ValueExpression);
        Assert.Equal("my-project", envVars[FlociUIContainerResource.GcpProjectEnvVar].ToString());
    }

    [Fact]
    public void WithReferenceBuilderShouldNotBeNull()
    {
        IResourceBuilder<FlociUIContainerResource> builder = null!;
        IResourceBuilder<FlociAzureContainerResource> azure = null!;
        Assert.Throws<ArgumentNullException>(() => builder.WithReference(azure));
    }
}

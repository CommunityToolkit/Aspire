using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class WithReferenceTests
{
    [Fact]
    public void WithReferenceBuilderShouldNotBeNull()
    {
        IResourceBuilder<ContainerResource> builder = null!;
        IResourceBuilder<FlociAwsContainerResource> floci = null!;
        Assert.Throws<ArgumentNullException>(() => builder.WithReference(floci));
    }

    [Fact]
    public void WithReferenceFlociResourceShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        var worker = builder.AddContainer("worker", "myorg/worker");

        Assert.Throws<ArgumentNullException>(() => worker.WithReference((IResourceBuilder<FlociAwsContainerResource>)null!));
    }

    [Fact]
    public async Task WithReferenceAwsSetsConnectionStringAndAwsSdkEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAws("floci", defaultRegion: "eu-west-1");
        var worker = builder.AddExecutable("worker", "dotnet", ".").WithReference(floci);

        var envVars = await ResolveEnvironmentAsync(builder, worker);

        Assert.Contains("ConnectionStrings__floci", envVars.Keys);
        Assert.Equal("eu-west-1", envVars["AWS_DEFAULT_REGION"].ToString());
        Assert.Equal("test", envVars["AWS_ACCESS_KEY_ID"].ToString());
        Assert.Equal("test", envVars["AWS_SECRET_ACCESS_KEY"].ToString());

        var endpointUrl = Assert.IsType<ReferenceExpression>(envVars["AWS_ENDPOINT_URL"]);
        Assert.Contains("floci.bindings.aws.host", endpointUrl.ValueExpression);
    }

    [Fact]
    public async Task WithReferenceAwsLeavesEndpointResolutionToAspireForContainerDependents()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAws("floci");
        var worker = builder.AddContainer("worker", "myorg/worker").WithReference(floci);

        var envVars = await ResolveEnvironmentAsync(builder, worker);

        // No hard-coded host.docker.internal: Aspire resolves the endpoint expression against the
        // container network when it materialises the dependent container's environment, so this
        // works on container runtimes that do not provide that alias.
        var endpointUrl = Assert.IsType<ReferenceExpression>(envVars["AWS_ENDPOINT_URL"]);
        Assert.Contains("floci.bindings.aws.host", endpointUrl.ValueExpression);
        Assert.DoesNotContain("host.docker.internal", endpointUrl.ValueExpression);
    }

    [Fact]
    public async Task WithReferenceAzureSetsBlobQueueAndTableEndpoints()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAzure("floci-az");
        var worker = builder.AddExecutable("worker", "dotnet", ".").WithReference(floci);

        var envVars = await ResolveEnvironmentAsync(builder, worker);

        Assert.Contains("ConnectionStrings__floci-az", envVars.Keys);

        var connectionString = Assert.IsType<ReferenceExpression>(envVars["AZURE_STORAGE_CONNECTION_STRING"]).ValueExpression;
        Assert.Contains($"AccountName={FlociAzureContainerResource.DefaultAccountName};", connectionString);
        Assert.Contains("BlobEndpoint=", connectionString);
        Assert.Contains("QueueEndpoint=", connectionString);
        Assert.Contains("TableEndpoint=", connectionString);
    }

    [Fact]
    public async Task WithReferenceGcpSetsEmulatorHostEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociGcp("floci-gcp", defaultProjectId: "my-project");
        var worker = builder.AddExecutable("worker", "dotnet", ".").WithReference(floci);

        var envVars = await ResolveEnvironmentAsync(builder, worker);

        Assert.Contains("ConnectionStrings__floci-gcp", envVars.Keys);
        Assert.Equal("my-project", envVars["GOOGLE_CLOUD_PROJECT"].ToString());
        Assert.Equal("my-project", envVars["CLOUDSDK_CORE_PROJECT"].ToString());

        foreach (var name in new[] { "PUBSUB_EMULATOR_HOST", "FIRESTORE_EMULATOR_HOST", "DATASTORE_EMULATOR_HOST", "SECRET_MANAGER_EMULATOR_HOST" })
        {
            var hostAndPort = Assert.IsType<ReferenceExpression>(envVars[name]).ValueExpression;
            Assert.DoesNotContain("http://", hostAndPort);
        }

        // The Storage SDK expects a full URL here, unlike the other emulator host variables. The
        // scheme is an endpoint expression so that configuring a certificate flips it to https.
        var storageHost = Assert.IsType<ReferenceExpression>(envVars["STORAGE_EMULATOR_HOST"]).ValueExpression;
        Assert.StartsWith("{floci-gcp.bindings.gcp.scheme}://", storageHost);
    }

    [Fact]
    public async Task WithCosmosCreatesChildResourceUsedByStandardWithReference()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAzure("floci-az");
        var cosmos = floci.WithCosmos();
        var worker = builder.AddExecutable("worker", "dotnet", ".").WithReference(cosmos);

        var envVars = await ResolveEnvironmentAsync(builder, worker);

        Assert.Equal("cosmos", cosmos.Resource.Name);
        Assert.Same(floci.Resource, cosmos.Resource.Parent);
        Assert.Contains(
            cosmos.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            annotation => annotation.Type == "Parent" && ReferenceEquals(annotation.Resource, floci.Resource));
        Assert.Contains("ConnectionStrings__cosmos", envVars.Keys);

        var connectionStringReference = Assert.IsType<ConnectionStringReference>(envVars["ConnectionStrings__cosmos"]);
        Assert.Same(cosmos.Resource, connectionStringReference.Resource);
        var connectionString = cosmos.Resource.ConnectionStringExpression.ValueExpression;
        Assert.Contains("AccountEndpoint=", connectionString);
        Assert.Contains($"/{FlociAzureContainerResource.DefaultAccountName}-cosmos/;", connectionString);
        Assert.Contains("AccountKey=", connectionString);
        // The endpoint is an unresolved expression so it tracks Aspire's (possibly randomized) port
        // assignment, and the scheme flips to https if a certificate is configured.
        Assert.Contains("{floci-az.bindings.azure.scheme}://", connectionString);
    }

    [Fact]
    public async Task WithCosmosComposesWithFlociAzureReference()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAzure("floci-az");
        var cosmos = floci.WithCosmos();
        var worker = builder.AddExecutable("worker", "dotnet", ".")
            .WithReference(floci)
            .WithReference(cosmos);

        var envVars = await ResolveEnvironmentAsync(builder, worker);

        // The base storage reference and the Cosmos connection string coexist.
        Assert.Contains("ConnectionStrings__floci-az", envVars.Keys);
        Assert.Contains("AZURE_STORAGE_CONNECTION_STRING", envVars.Keys);
        Assert.Contains("ConnectionStrings__cosmos", envVars.Keys);
    }

    [Fact]
    public async Task WithCosmosHonorsCustomResourceAndAccountName()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var floci = builder.AddFlociAzure("floci-az");
        var cosmos = floci.WithCosmos(name: "notifications", accountName: "acct2");
        var worker = builder.AddExecutable("worker", "dotnet", ".").WithReference(cosmos);

        var envVars = await ResolveEnvironmentAsync(builder, worker);

        Assert.Contains("ConnectionStrings__notifications", envVars.Keys);
        var connectionStringReference = Assert.IsType<ConnectionStringReference>(envVars["ConnectionStrings__notifications"]);
        Assert.Same(cosmos.Resource, connectionStringReference.Resource);
        var connectionString = cosmos.Resource.ConnectionStringExpression.ValueExpression;
        Assert.Contains("/acct2-cosmos/;", connectionString);
    }

    [Fact]
    public void WithCosmosBuilderShouldNotBeNull()
    {
        IResourceBuilder<FlociAzureContainerResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithCosmos());
    }

    [Fact]
    public void WithCosmosResourceNameShouldNotBeEmpty()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        var floci = builder.AddFlociAzure("floci-az");

        Assert.Throws<ArgumentException>(() => floci.WithCosmos(GetInvalidResourceName()));
    }

    private static string GetInvalidResourceName() => string.Empty;

    private static async Task<Dictionary<string, object>> ResolveEnvironmentAsync<T>(
        IDistributedApplicationBuilder builder,
        IResourceBuilder<T> dependent)
        where T : IResource
    {
        Assert.True(dependent.Resource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        return envVars;
    }
}

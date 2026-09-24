using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class AzureServiceBusResourceTests
{
    [Fact]
    public void WithServiceBusCreatesChildResourceWithAspireAllocatedEndpoints()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        azure.WithServiceBus();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var serviceBus = Assert.Single(appModel.Resources.OfType<FlociAzureServiceBusResource>());
        Assert.Equal("servicebus", serviceBus.Name);
        Assert.Same(azure.Resource, serviceBus.Parent);

        Assert.Collection(
            serviceBus.Annotations.OfType<EndpointAnnotation>().OrderBy(endpoint => endpoint.Name),
            amqp => AssertEndpoint(amqp, "amqp", "sb", null),
            amqps => AssertEndpoint(amqps, "amqps", "amqps", null));
    }

    [Fact]
    public void WithServiceBusHonorsExplicitPorts()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var serviceBus = builder.AddFlociAzure("floci-az")
            .WithServiceBus(amqpPort: 5673, amqpTlsPort: 5674);

        Assert.Equal(5673, serviceBus.Resource.AmqpEndpoint.EndpointAnnotation.Port);
        Assert.Equal(5674, serviceBus.Resource.AmqpTlsEndpoint.EndpointAnnotation.Port);
    }

    [Fact]
    public void EqualListenerPortsThrowBeforeRegisteringChild()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        var azure = builder.AddFlociAzure("floci-az");

        var exception = Assert.Throws<ArgumentException>(() => azure.WithServiceBus(amqpPort: 5673, amqpTlsPort: 5673));

        Assert.Equal("amqpTlsPort", exception.ParamName);
        Assert.DoesNotContain(builder.Resources, resource => resource is FlociAzureServiceBusResource);
    }

    [Fact]
    public void PublishModeRejectsServiceBusBeforeRegisteringChild()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var azure = builder.AddFlociAzure("floci-az");

        var exception = Assert.Throws<NotSupportedException>(() => azure.WithServiceBus());

        Assert.Contains("ExecutionContext.IsRunMode", exception.Message);
        Assert.DoesNotContain(builder.Resources, resource => resource is FlociAzureServiceBusResource);
    }

    [Fact]
    public async Task WithServiceBusUsesAllocatedEndpointPortsForTheDataPlane()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus();
        AllocateEndpoints(serviceBus.Resource, 5673, 5674);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<FlociAzureContainerResource>());
        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        Assert.Equal("false", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_MOCKED"]);
        Assert.Equal("true", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_START_ON_BOOT"]);
        Assert.Equal("5673", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_PORT"]);
        Assert.Equal("5674", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_TLS_PORT"]);
    }

    [Fact]
    public async Task WithServiceBusDoesNotConfigureTheDataPlaneInPublishMode()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        azure.WithServiceBus();

        var envVars = new Dictionary<string, object>();
        var executionContext = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Publish));
        var context = new EnvironmentCallbackContext(executionContext, envVars);

        foreach (var annotation in azure.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        Assert.DoesNotContain("FLOCI_AZ_SERVICES_SERVICE_BUS_MOCKED", envVars);
        Assert.DoesNotContain("FLOCI_AZ_SERVICES_SERVICE_BUS_START_ON_BOOT", envVars);
        Assert.DoesNotContain("FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_PORT", envVars);
        Assert.DoesNotContain("FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_TLS_PORT", envVars);
    }

    [Fact]
    public async Task ConnectionStringMatchesTheEmulatorShape()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var serviceBus = builder.AddFlociAzure("floci-az").WithServiceBus();
        AllocateEndpoints(serviceBus.Resource, 5673, 5674);

        string? connectionString = await serviceBus.Resource.ConnectionStringExpression
            .GetValueAsync(CancellationToken.None);

        Assert.Equal(
            "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            connectionString);
    }

    [Fact]
    public void SecondWithServiceBusReturnsTheExistingChild()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        var first = azure.WithServiceBus(amqpPort: 5673);
        var second = azure.WithServiceBus(amqpPort: 5673);

        Assert.Same(first.Resource, second.Resource);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Single(appModel.Resources.OfType<FlociAzureServiceBusResource>());
    }

    [Fact]
    public void ConflictingPortsThrow()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        azure.WithServiceBus(amqpPort: 5673);

        Assert.Throws<InvalidOperationException>(() => azure.WithServiceBus(amqpPort: 5675));
    }

    [Fact]
    public void ConflictingNamesThrow()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus("first");

        Assert.Throws<InvalidOperationException>(() => azure.WithServiceBus("second"));
        Assert.Same(serviceBus.Resource, azure.WithServiceBus("FIRST").Resource);
        Assert.Single(builder.Resources.OfType<FlociAzureServiceBusResource>());
    }

    [Fact]
    public async Task MultipleEmulatorsUseDistinctChildNamesAndConnectionKeys()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        var first = builder.AddFlociAzure("first").WithServiceBus();
        var second = builder.AddFlociAzure("second").WithServiceBus("second-servicebus");
        AllocateEndpoints(first.Resource, 5673, 5674);
        AllocateEndpoints(second.Resource, 5675, 5676);
        var consumer = builder.AddExecutable("consumer", "dotnet", ".")
            .WithReference(first)
            .WithReference(second);

        using var app = builder.Build();
        var environment = await consumer.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Contains("Endpoint=sb://localhost:5673;", environment["ConnectionStrings__servicebus"]);
        Assert.Contains("Endpoint=sb://localhost:5675;", environment["ConnectionStrings__second-servicebus"]);
        Assert.Equal(2, builder.Resources.OfType<FlociAzureServiceBusResource>().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithReferenceInjectsTheConnectionString(bool useContainer)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var serviceBus = builder.AddFlociAzure("floci-az").WithServiceBus();
        AllocateEndpoints(serviceBus.Resource, 5673, 5674);

        IResourceBuilder<IResourceWithEnvironment> consumer = useContainer
            ? builder.AddContainer("api", "my-api-image").WithReference(serviceBus)
            : builder.AddExecutable("api", "dotnet", ".").WithReference(serviceBus);

        using var app = builder.Build();

        Assert.True(consumer.Resource.TryGetAnnotationsOfType(
            out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        object connectionString = envVars["ConnectionStrings__servicebus"];
        string? value = connectionString is IValueProvider provider
            ? await provider.GetValueAsync(CancellationToken.None)
            : connectionString.ToString();

        Assert.Equal(
            $"Endpoint=sb://{(useContainer ? "floci-servicebus-host.internal" : "localhost")}:5673;SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            value);
    }

    [Fact]
    public async Task ContainerReferencePreservesCustomConnectionNameAndAvoidsHostTunnelDependency()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus();
        var consumer = builder.AddContainer("api", "my-api-image")
            .WithReference(serviceBus, connectionName: "messages");

        // Dependency discovery runs before allocation and must not resolve the host ports
        // or request an Aspire tunnel for the sidecar's host-only endpoints.
        var dependencies = await consumer.Resource.GetResourceDependenciesAsync(builder.ExecutionContext,
            new ResourceDependencyDiscoveryOptions { DiscoveryMode = ResourceDependencyDiscoveryMode.DirectOnly });
        Assert.Contains(azure.Resource, dependencies);
        Assert.DoesNotContain(serviceBus.Resource, dependencies);

        AllocateEndpoints(serviceBus.Resource, 5673, 5674);
        await using var app = await builder.BuildAsync();
        var environment = await consumer.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);
        Assert.Contains("Endpoint=sb://floci-servicebus-host.internal:5673;", environment["ConnectionStrings__messages"]);
        Assert.DoesNotContain("ConnectionStrings__servicebus", environment);

        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            await consumer.Resource.GetEnvironmentVariablesAsync(DistributedApplicationOperation.Publish, app.Services));
        Assert.IsType<NotSupportedException>(Assert.Single(exception.InnerExceptions));
    }

    private static void AssertEndpoint(
        EndpointAnnotation endpoint,
        string name,
        string scheme,
        int? port)
    {
        Assert.Equal(name, endpoint.Name);
        Assert.Equal(scheme, endpoint.UriScheme);
        Assert.Equal(port, endpoint.Port);
        Assert.False(endpoint.IsExplicitlyProxied);
    }

    private static void AllocateEndpoints(
        FlociAzureServiceBusResource serviceBus,
        int amqpPort,
        int amqpTlsPort)
    {
        serviceBus.AmqpEndpoint.EndpointAnnotation.AllocatedEndpoint =
            new AllocatedEndpoint(serviceBus.AmqpEndpoint.EndpointAnnotation, "localhost", amqpPort);
        serviceBus.AmqpTlsEndpoint.EndpointAnnotation.AllocatedEndpoint =
            new AllocatedEndpoint(serviceBus.AmqpTlsEndpoint.EndpointAnnotation, "localhost", amqpTlsPort);
    }
}

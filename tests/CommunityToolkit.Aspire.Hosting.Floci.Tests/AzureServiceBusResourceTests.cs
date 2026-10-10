using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class AzureServiceBusResourceTests
{
    [Fact]
    public void WithServiceBusCreatesChildResourceWithUnallocatedEndpoints()
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

    [Theory]
    [InlineData(null, null)]
    [InlineData(5673, null)]
    [InlineData(null, 5674)]
    [InlineData(5673, 5674)]
    public async Task WithServiceBusAllocatesEndpointsBeforeConfiguringTheDataPlane(int? configuredAmqpPort, int? configuredAmqpTlsPort)
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus(
            amqpPort: configuredAmqpPort,
            amqpTlsPort: configuredAmqpTlsPort);

        var dependencies = await azure.Resource.GetResourceDependenciesAsync(builder.ExecutionContext,
            new ResourceDependencyDiscoveryOptions { DiscoveryMode = ResourceDependencyDiscoveryMode.DirectOnly });
        Assert.DoesNotContain(serviceBus.Resource, dependencies);

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var allocator = Assert.Single(app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<FlociServiceBusEndpointAllocator>());
        await allocator.SubscribeAsync(builder.Eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);

        await builder.Eventing.PublishAsync(
            new BeforeStartEvent(app.Services, appModel), TestContext.Current.CancellationToken);

        string? amqpPort = await serviceBus.Resource.AmqpEndpoint.Property(EndpointProperty.Port)
            .GetValueAsync(TestContext.Current.CancellationToken);
        string? amqpTlsPort = await serviceBus.Resource.AmqpTlsEndpoint.Property(EndpointProperty.Port)
            .GetValueAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(amqpPort, amqpTlsPort);
        foreach (var endpoint in new[] { serviceBus.Resource.AmqpEndpoint, serviceBus.Resource.AmqpTlsEndpoint })
        {
            var containerAllocation = await endpoint.EndpointAnnotation.AllAllocatedEndpoints.GetAllocatedEndpointAsync(
                KnownNetworkIdentifiers.DefaultAspireContainerNetwork, TestContext.Current.CancellationToken);
            Assert.Equal(FlociAzureServiceBusResource.ContainerHost, containerAllocation.Address);
            Assert.Equal(await endpoint.Property(EndpointProperty.Port).GetValueAsync(TestContext.Current.CancellationToken),
                containerAllocation.Port.ToString());
        }
        if (configuredAmqpPort.HasValue)
        {
            Assert.Equal(configuredAmqpPort.Value.ToString(), amqpPort);
        }
        else
        {
            // Selected ports stay below every OS ephemeral range, where DCP allocates proxy ports.
            Assert.InRange(int.Parse(amqpPort!), 20000, 32767);
        }
        if (configuredAmqpTlsPort.HasValue)
        {
            Assert.Equal(configuredAmqpTlsPort.Value.ToString(), amqpTlsPort);
        }
        else
        {
            Assert.InRange(int.Parse(amqpTlsPort!), 20000, 32767);
        }

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
        Assert.Equal(EndpointProperty.TargetPort,
            Assert.IsType<EndpointReferenceExpression>(envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_PORT"]).Property);
        Assert.Equal(EndpointProperty.TargetPort,
            Assert.IsType<EndpointReferenceExpression>(envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_TLS_PORT"]).Property);
        Assert.Equal(amqpPort, await ((IValueProvider)envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_PORT"])
            .GetValueAsync(TestContext.Current.CancellationToken));
        Assert.Equal(amqpTlsPort, await ((IValueProvider)envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_TLS_PORT"])
            .GetValueAsync(TestContext.Current.CancellationToken));
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
        using var builder = TestDistributedApplicationBuilder.Create();
        var first = builder.AddFlociAzure("first").WithServiceBus();
        var second = builder.AddFlociAzure("second").WithServiceBus("second-servicebus");
        var consumer = builder.AddExecutable("consumer", "dotnet", ".")
            .WithReference(first)
            .WithReference(second);

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        var allocator = Assert.Single(app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<FlociServiceBusEndpointAllocator>());
        await allocator.SubscribeAsync(builder.Eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
        await builder.Eventing.PublishAsync(
            new BeforeStartEvent(app.Services, app.Services.GetRequiredService<DistributedApplicationModel>()),
            TestContext.Current.CancellationToken);
        var environment = await consumer.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        string? firstPort = await first.Resource.AmqpEndpoint.Property(EndpointProperty.Port)
            .GetValueAsync(TestContext.Current.CancellationToken);
        string? secondPort = await second.Resource.AmqpEndpoint.Property(EndpointProperty.Port)
            .GetValueAsync(TestContext.Current.CancellationToken);
        Assert.Contains($"Endpoint=sb://localhost:{firstPort};", environment["ConnectionStrings__servicebus"]);
        Assert.Contains($"Endpoint=sb://localhost:{secondPort};", environment["ConnectionStrings__second-servicebus"]);
        string?[] ports = [firstPort, secondPort,
            await first.Resource.AmqpTlsEndpoint.Property(EndpointProperty.Port).GetValueAsync(TestContext.Current.CancellationToken),
            await second.Resource.AmqpTlsEndpoint.Property(EndpointProperty.Port).GetValueAsync(TestContext.Current.CancellationToken)];
        Assert.Equal(4, ports.Distinct().Count());
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
            ? await provider.GetValueAsync(new ValueProviderContext
            {
                ExecutionContext = builder.ExecutionContext,
                Caller = consumer.Resource,
                Network = useContainer ? KnownNetworkIdentifiers.DefaultAspireContainerNetwork : KnownNetworkIdentifiers.LocalhostNetwork
            }, TestContext.Current.CancellationToken)
            : connectionString.ToString();

        Assert.Equal(
            $"Endpoint=sb://{(useContainer ? FlociAzureServiceBusResource.ContainerHost : KnownHostNames.Localhost)}:5673;SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            value);
    }

    [Fact]
    public async Task ContainerReferencePreservesCustomConnectionNameAndAvoidsChildTunnelDependency()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus();
        var consumer = builder.AddContainer("api", "my-api-image")
            .WithReference(serviceBus, connectionName: "messages");

        // The sidecar is configured by its owner and is already reachable through the host gateway.
        var dependencies = await consumer.Resource.GetResourceDependenciesAsync(builder.ExecutionContext,
            new ResourceDependencyDiscoveryOptions { DiscoveryMode = ResourceDependencyDiscoveryMode.DirectOnly });
        Assert.Contains(azure.Resource, dependencies);
        Assert.DoesNotContain(serviceBus.Resource, dependencies);

        AllocateEndpoints(serviceBus.Resource, 5673, 5674);
        await using var app = await builder.BuildAsync();
        var environment = await consumer.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);
        Assert.Contains($"Endpoint=sb://{FlociAzureServiceBusResource.ContainerHost}:5673;", environment["ConnectionStrings__messages"]);
        Assert.DoesNotContain("ConnectionStrings__servicebus", environment);

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
        foreach (var (endpoint, port) in new[]
        {
            (serviceBus.AmqpEndpoint.EndpointAnnotation, amqpPort),
            (serviceBus.AmqpTlsEndpoint.EndpointAnnotation, amqpTlsPort)
        })
        {
            endpoint.TargetPort = port;
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, KnownHostNames.Localhost, port);
            endpoint.AllAllocatedEndpoints.AddOrUpdateAllocatedEndpoint(
                KnownNetworkIdentifiers.DefaultAspireContainerNetwork,
                new AllocatedEndpoint(endpoint, FlociAzureServiceBusResource.ContainerHost, port,
                    EndpointBindingMode.SingleAddress, null, KnownNetworkIdentifiers.DefaultAspireContainerNetwork));
        }
    }
}

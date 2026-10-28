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
    public async Task WithServiceBusPassesConfiguredPortsAndLetsDockerAssignTheRest(int? configuredAmqpPort, int? configuredAmqpTlsPort)
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
        await StartAllocatorAsync(builder, app);

        // Configured ports are allocated before floci-az starts; the rest wait for Docker to assign them.
        foreach (var (endpoint, configuredPort) in new[]
        {
            (serviceBus.Resource.AmqpEndpoint, configuredAmqpPort),
            (serviceBus.Resource.AmqpTlsEndpoint, configuredAmqpTlsPort)
        })
        {
            if (configuredPort is null)
            {
                Assert.Null(endpoint.EndpointAnnotation.AllocatedEndpoint);
                continue;
            }

            Assert.Equal(configuredPort.Value.ToString(), await endpoint.Property(EndpointProperty.Port)
                .GetValueAsync(TestContext.Current.CancellationToken));
            var containerAllocation = await endpoint.EndpointAnnotation.AllAllocatedEndpoints.GetAllocatedEndpointAsync(
                KnownNetworkIdentifiers.DefaultAspireContainerNetwork, TestContext.Current.CancellationToken);
            Assert.Equal(FlociAzureServiceBusResource.ContainerHost, containerAllocation.Address);
            Assert.Equal(configuredPort.Value, containerAllocation.Port);
        }

        var envVars = await azure.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);

        Assert.Equal("false", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_MOCKED"]);
        Assert.Equal("true", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_START_ON_BOOT"]);
        Assert.Equal((configuredAmqpPort ?? 0).ToString(), envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_PORT"]);
        Assert.Equal((configuredAmqpTlsPort ?? 0).ToString(), envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_TLS_PORT"]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(5673, null)]
    [InlineData(null, 5674)]
    public async Task ParentReadyAllocatesDockerAssignedPortsFromTheNamespaceApi(int? configuredAmqpPort, int? configuredAmqpTlsPort)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var namespaceApi = new NamespaceApiStub();
        namespaceApi.Respond("localhost:4577", """{"name":"default","amqpPort":5672,"amqpsPort":5671,"amqpHostPort":21001,"amqpsHostPort":21002,"mocked":false}""");
        builder.Services.AddHttpClient(FlociServiceBusEndpointAllocator.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => namespaceApi);

        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus(amqpPort: configuredAmqpPort, amqpTlsPort: configuredAmqpTlsPort);
        var connectionStringAvailable = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(serviceBus.Resource, (_, _) =>
        {
            connectionStringAvailable.TrySetResult();
            return Task.CompletedTask;
        });

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        await StartAllocatorAsync(builder, app);
        Assert.Equal(KnownResourceStates.Waiting, GetState(app, serviceBus.Resource));
        Assert.False(connectionStringAvailable.Task.IsCompleted);

        AllocateParent(azure.Resource, 4577);
        await builder.Eventing.PublishAsync(new ResourceReadyEvent(azure.Resource, app.Services),
            TestContext.Current.CancellationToken);

        Assert.Equal("http://localhost:4577/devstoreaccount1-servicebus/namespaces/default",
            Assert.Single(namespaceApi.Requests).ToString());
        Assert.Equal((configuredAmqpPort ?? 21001).ToString(), await serviceBus.Resource.AmqpEndpoint
            .Property(EndpointProperty.Port).GetValueAsync(TestContext.Current.CancellationToken));
        Assert.Equal((configuredAmqpTlsPort ?? 21002).ToString(), await serviceBus.Resource.AmqpTlsEndpoint
            .Property(EndpointProperty.Port).GetValueAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            $"Endpoint=sb://localhost:{configuredAmqpPort ?? 21001};SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            await serviceBus.Resource.ConnectionStringExpression.GetValueAsync(TestContext.Current.CancellationToken));
        Assert.True(connectionStringAvailable.Task.IsCompleted);
        Assert.Equal(KnownResourceStates.Running, GetState(app, serviceBus.Resource));

        await builder.Eventing.PublishAsync(
            new ResourceStoppedEvent(azure.Resource, app.Services, new ResourceEvent(azure.Resource, azure.Resource.Name,
                new CustomResourceSnapshot { ResourceType = "Container", Properties = [] })),
            TestContext.Current.CancellationToken);
        Assert.Equal(KnownResourceStates.Exited, GetState(app, serviceBus.Resource));
    }

    [Fact]
    public async Task ParentReadyWithConfiguredPortsDoesNotQueryTheNamespaceApi()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var namespaceApi = new NamespaceApiStub();
        builder.Services.AddHttpClient(FlociServiceBusEndpointAllocator.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => namespaceApi);
        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus(amqpPort: 5673, amqpTlsPort: 5674);

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        await StartAllocatorAsync(builder, app);
        AllocateParent(azure.Resource, 4577);
        await builder.Eventing.PublishAsync(new ResourceReadyEvent(azure.Resource, app.Services),
            TestContext.Current.CancellationToken);

        Assert.Empty(namespaceApi.Requests);
        Assert.Equal(KnownResourceStates.Running, GetState(app, serviceBus.Resource));
    }

    [Fact]
    public async Task ParentReadyMarksTheChildFailedWhenPortsCannotBeDiscovered()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var namespaceApi = new NamespaceApiStub();
        namespaceApi.Respond("localhost:4577", """{"name":"default","amqpPort":0,"amqpsPort":0,"amqpHostPort":0,"amqpsHostPort":0,"mocked":true}""");
        builder.Services.AddHttpClient(FlociServiceBusEndpointAllocator.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => namespaceApi);
        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus();

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        await StartAllocatorAsync(builder, app);
        var pendingContainerConsumer = serviceBus.Resource.AmqpEndpoint.EndpointAnnotation.AllAllocatedEndpoints
            .GetAllocatedEndpointAsync(KnownNetworkIdentifiers.DefaultAspireContainerNetwork, TestContext.Current.CancellationToken);
        AllocateParent(azure.Resource, 4577);
        await builder.Eventing.PublishAsync(new ResourceReadyEvent(azure.Resource, app.Services),
            TestContext.Current.CancellationToken);

        Assert.Equal(KnownResourceStates.FailedToStart, GetState(app, serviceBus.Resource));
        // Consumers resolving the connection string fail instead of waiting forever.
        await Assert.ThrowsAsync<DistributedApplicationException>(() => pendingContainerConsumer);
        await Assert.ThrowsAsync<DistributedApplicationException>(() => serviceBus.Resource.ConnectionStringExpression
            .GetValueAsync(TestContext.Current.CancellationToken).AsTask());
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(5673, true)]
    [InlineData(5673, false)]
    public async Task ContainersWaitForTheEmulatorWhileServiceBusPortsAreUnknown(int? configuredAmqpPort, bool dockerAssignsAmqpTlsPort)
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus(amqpPort: configuredAmqpPort, amqpTlsPort: dockerAssignsAmqpTlsPort ? null : 5674);
        var container = builder.AddContainer("api", "my-api-image").WithReference(serviceBus).WaitFor(serviceBus);
        var executable = builder.AddExecutable("worker", "dotnet", ".").WithReference(serviceBus).WaitFor(serviceBus);

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        await StartAllocatorAsync(builder, app);

        // DCP needs a container's host dependencies' addresses when it creates the container, so containers wait for
        // the emulator until Docker has assigned the ports. Executables keep waiting for the Service Bus resource.
        bool portsUnknown = configuredAmqpPort is null || dockerAssignsAmqpTlsPort;
        var containerWait = Assert.Single(container.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(portsUnknown ? azure.Resource : serviceBus.Resource, containerWait.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, containerWait.WaitType);
        Assert.Same(serviceBus.Resource, Assert.Single(executable.Resource.Annotations.OfType<WaitAnnotation>()).Resource);
    }

    [Fact]
    public void ParseNamespacePortsReadsTheReportedHostPorts()
    {
        Assert.Equal((21001, 21002), FlociServiceBusEndpointAllocator.ParseNamespacePorts(
            """{"name":"default","amqpPort":5672,"amqpsPort":5671,"amqpHostPort":21001,"amqpsHostPort":21002,"tlsCertPem":"","mocked":false}"""));
    }

    [Theory]
    [InlineData("""{"name":"default","amqpPort":5672,"amqpsPort":5671}""")]
    [InlineData("""{"name":"default","amqpHostPort":0,"amqpsHostPort":21002}""")]
    [InlineData("""{"name":"default","amqpHostPort":21001,"amqpsHostPort":"21002"}""")]
    [InlineData("""[]""")]
    public void ParseNamespacePortsRejectsMissingPorts(string json)
    {
        Assert.Throws<DistributedApplicationException>(() => FlociServiceBusEndpointAllocator.ParseNamespacePorts(json));
    }

    [Fact]
    public void ServiceBusIsLinkedToItsParentByRelationship()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus();

        var relationship = Assert.Single(serviceBus.Resource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            annotation => annotation.Type == "Parent");
        Assert.Same(azure.Resource, relationship.Resource);
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
        var namespaceApi = new NamespaceApiStub();
        namespaceApi.Respond("localhost:4577", """{"amqpPort":5672,"amqpsPort":5671,"amqpHostPort":21001,"amqpsHostPort":21002}""");
        namespaceApi.Respond("localhost:4578", """{"amqpPort":5672,"amqpsPort":5671,"amqpHostPort":21003,"amqpsHostPort":21004}""");
        builder.Services.AddHttpClient(FlociServiceBusEndpointAllocator.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => namespaceApi);
        var first = builder.AddFlociAzure("first");
        var firstServiceBus = first.WithServiceBus();
        var second = builder.AddFlociAzure("second");
        var secondServiceBus = second.WithServiceBus("second-servicebus");
        var consumer = builder.AddExecutable("consumer", "dotnet", ".")
            .WithReference(firstServiceBus)
            .WithReference(secondServiceBus);

        await using var app = await builder.BuildAsync(TestContext.Current.CancellationToken);
        await StartAllocatorAsync(builder, app);
        foreach (var (parent, port) in new[] { (first.Resource, 4577), (second.Resource, 4578) })
        {
            AllocateParent(parent, port);
            await builder.Eventing.PublishAsync(new ResourceReadyEvent(parent, app.Services),
                TestContext.Current.CancellationToken);
        }

        var environment = await consumer.Resource.GetEnvironmentVariablesAsync(serviceProvider: app.Services);
        Assert.Contains("Endpoint=sb://localhost:21001;", environment["ConnectionStrings__servicebus"]);
        Assert.Contains("Endpoint=sb://localhost:21003;", environment["ConnectionStrings__second-servicebus"]);
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

    private static async Task StartAllocatorAsync(IDistributedApplicationTestingBuilder builder, DistributedApplication app)
    {
        var allocator = Assert.Single(app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<FlociServiceBusEndpointAllocator>());
        await allocator.SubscribeAsync(builder.Eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
        await builder.Eventing.PublishAsync(
            new BeforeStartEvent(app.Services, app.Services.GetRequiredService<DistributedApplicationModel>()),
            TestContext.Current.CancellationToken);
    }

    private static void AllocateParent(FlociAzureContainerResource parent, int port)
    {
        var endpoint = parent.PrimaryEndpoint.EndpointAnnotation;
        endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, KnownHostNames.Localhost, port);
    }

    private static string? GetState(DistributedApplication app, IResource resource)
    {
        Assert.True(app.ResourceNotifications.TryGetCurrentState(resource.Name, out var resourceEvent));
        return resourceEvent.Snapshot.State?.Text;
    }

    private sealed class NamespaceApiStub : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = [];

        public List<Uri> Requests { get; } = [];

        public void Respond(string authority, string json) => _responses[authority] = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(_responses.TryGetValue(request.RequestUri!.Authority, out string? json)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
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

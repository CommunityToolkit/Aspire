using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

public class AzureServiceBusResourceTests
{
    [Fact]
    public void WithServiceBusCreatesChildResourceWithDefaults()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        azure.WithServiceBus();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var serviceBus = appModel.Resources.OfType<FlociAzureServiceBusResource>().SingleOrDefault();

        Assert.NotNull(serviceBus);
        Assert.Equal("servicebus", serviceBus.Name);
        Assert.Same(azure.Resource, serviceBus.Parent);
        Assert.InRange(serviceBus.AmqpPort, 1, 65535);
        Assert.InRange(serviceBus.AmqpTlsPort, 1, 65535);
        Assert.NotEqual(serviceBus.AmqpPort, serviceBus.AmqpTlsPort);
    }

    [Fact]
    public void WithServiceBusHonorsExplicitPorts()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var serviceBus = builder.AddFlociAzure("floci-az")
            .WithServiceBus(amqpPort: 5673, amqpTlsPort: 5674);

        Assert.Equal(5673, serviceBus.Resource.AmqpPort);
        Assert.Equal(5674, serviceBus.Resource.AmqpTlsPort);
    }

    [Fact]
    public async Task WithServiceBusEnablesTheDataPlaneOnTheEmulator()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var azure = builder.AddFlociAzure("floci-az");
        var serviceBus = azure.WithServiceBus(amqpPort: 5673, amqpTlsPort: 5674);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<FlociAzureContainerResource>().Single();
        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EnvironmentCallbackAnnotation>? envAnnotations));

        var envVars = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(builder.ExecutionContext, envVars);
        foreach (var annotation in envAnnotations!)
        {
            await annotation.Callback(context);
        }

        Assert.Equal("false", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_MOCKED"].ToString());
        Assert.Equal("true", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_START_ON_BOOT"].ToString());
        Assert.Equal("5673", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_PORT"].ToString());
        Assert.Equal("5674", envVars["FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_TLS_PORT"].ToString());
        Assert.Equal(5673, serviceBus.Resource.AmqpPort);
    }

    [Fact]
    public async Task ConnectionStringMatchesTheEmulatorShape()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var serviceBus = builder.AddFlociAzure("floci-az")
            .WithServiceBus(amqpPort: 5673, amqpTlsPort: 5674);

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
        var second = azure.WithServiceBus();

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
    public async Task WithReferenceInjectsTheConnectionString()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var serviceBus = builder.AddFlociAzure("floci-az")
            .WithServiceBus(amqpPort: 5673, amqpTlsPort: 5674);

        var consumer = builder.AddContainer("api", "my-api-image")
            .WithReference(serviceBus);

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
            "Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
            value);
    }
}

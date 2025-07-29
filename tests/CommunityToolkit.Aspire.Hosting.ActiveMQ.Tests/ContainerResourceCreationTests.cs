using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddActiveMqApiBuilderBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddActiveMQ("amq"));
    }

    [Fact]
    public void AddActiveMqApiBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddActiveMQ(null!));
    }

    [Fact]
    public void AddActiveMqApiBuilderSchemeShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddActiveMQ("amq",
            scheme: null!));
    }

    [Fact]
    public void AddActiveMqApiBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddActiveMQ("amq",
            builder.AddParameter("username", "admin"),
            builder.AddParameter("password", "admin"));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<ActiveMQServerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("amq", resource.Name);
        Assert.Equal("admin", resource.UserNameParameter!.Value);
        Assert.Equal("admin", resource.PasswordParameter.Value);
        Assert.Equal("ACTIVEMQ_CONNECTION_PASSWORD", resource.ActiveMqSettings.EnvironmentVariablePassword);
        Assert.Equal("ACTIVEMQ_CONNECTION_USER", resource.ActiveMqSettings.EnvironmentVariableUsername);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal("6.1.4", imageAnnotations.Tag);
        Assert.Equal("apache/activemq-classic", imageAnnotations.Image);
        Assert.Equal("docker.io", imageAnnotations.Registry);

        var endpoint = resource.PrimaryEndpoint;
        Assert.Equal(61616, endpoint.TargetPort);
    }

    [Fact]
    public void AddActiveMqArtemisApiBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddActiveMQArtemis("amq",
            builder.AddParameter("username", "admin"),
            builder.AddParameter("password", "admin"));

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<ActiveMQArtemisServerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("amq", resource.Name);
        Assert.Equal("admin", resource.UserNameParameter!.Value);
        Assert.Equal("admin", resource.PasswordParameter.Value);
        Assert.Equal("ARTEMIS_PASSWORD", resource.ActiveMqSettings.EnvironmentVariablePassword);
        Assert.Equal("ARTEMIS_USER", resource.ActiveMqSettings.EnvironmentVariableUsername);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal("2.39.0", imageAnnotations.Tag);
        Assert.Equal("apache/activemq-artemis", imageAnnotations.Image);
        Assert.Equal("docker.io", imageAnnotations.Registry);

        var endpoint = resource.PrimaryEndpoint;
        Assert.Equal(61616, endpoint.TargetPort);
    }
}

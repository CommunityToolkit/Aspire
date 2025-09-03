using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.Tests;
public class ContainerResourceCreationTests
{
    [Fact]
    public void AddDataAPIBuilderBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<NullReferenceException>(() => builder.AddDataAPIBuilder("dab"));
    }

    [Fact]
    public void AddDataApiBuilderNameShouldNotBeNullOrWhiteSpace()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataAPIBuilder(null!));
    }

    [Fact]
    public void AddDataAPIBuilderContainerDetailsSetOnResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<DataApiBuilderContainerResource>().SingleOrDefault();

        Assert.NotNull(resource);
        Assert.Equal("dab", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));

        // verify ports

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));

        var http = endpoints.Where(x => x.Name == DataApiBuilderContainerResource.HttpEndpointName).Single();
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, http.TargetPort);

        // var https = endpoints.Where(x => x.Name == DataApiBuilderContainerResource.HttpsEndpointName).Single();
        // Assert.Equal(DataApiBuilderContainerResource.HttpsEndpointPort, https.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_DefaultFile_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // defaults to ./dab-config.json which exists in this test project root
        builder.AddDataAPIBuilder("dab");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var annotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("dab-config.json", annotation.Source);
        Assert.Equal("/App/dab-config.json", annotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_PortOnly_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        builder.AddDataAPIBuilder("dab", httpPort: 1234);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpointAnnotations));

        var annotation = Assert.Single(endpointAnnotations);
        Assert.Equal(1234, annotation.Port);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, annotation.TargetPort);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFile_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", configFilePaths: "./dab-config.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var annotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("dab-config.json", annotation.Source);
        Assert.Equal("/App/dab-config.json", annotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFileWithPort_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file exists in test project root
        builder.AddDataAPIBuilder("dab", httpPort: 1234, configFilePaths: "./dab-config.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpointAnnotations));

        var annotation = Assert.Single(endpointAnnotations);
        Assert.Equal(1234, annotation.Port);
        Assert.Equal(DataApiBuilderContainerResource.HttpEndpointPort, annotation.TargetPort);

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        var configAnnotation = Assert.Single(configFileAnnotations);
        Assert.EndsWith("dab-config.json", configAnnotation.Source);
        Assert.Equal("/App/dab-config.json", configAnnotation.Target);
    }

    [Fact]
    public void AddDataAPIBuilderContainer_InvalidFile_ThrowsEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // file does not exist in test project root
        Assert.Throws<FileNotFoundException>(() => builder.AddDataAPIBuilder("dab", configFilePaths: Guid.NewGuid().ToString()));
    }

    [Fact]
    public void AddDataAPIBuilderContainer_ValidFiles_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // both files exist in test project root
        builder.AddDataAPIBuilder("dab", "./dab-config.json", "./dab-config-2.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderContainerResource>());

        Assert.True(resource.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var configFileAnnotations));

        Assert.Equal(2, configFileAnnotations.Count());
        Assert.Collection(
            configFileAnnotations,
            a =>
            {
                Assert.EndsWith("dab-config.json", a.Source);
                Assert.Equal("/App/dab-config.json", a.Target);
            },
            a =>
            {
                Assert.EndsWith("dab-config-2.json", a.Source);
                Assert.Equal("/App/dab-config-2.json", a.Target);
            });
    }

    [Fact]
    public void AddDataAPIBuilderContainer_InvalidFiles_NoEx()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        // some (not all) files exist in test project root
        Assert.Throws<FileNotFoundException>(() => builder.AddDataAPIBuilder("dab", "./dab-config.json", "./dab-config-2.json", Guid.NewGuid().ToString()));
    }

    [Fact]
    public void RunAsExecutable_InDevelopmentMode_CreatesExecutableResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var containerBuilder = builder.AddDataAPIBuilder("dab", "./dab-config.json");
        var executableBuilder = containerBuilder.RunAsExecutable();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderExecutableResource>());
        Assert.Equal("dab", resource.Name);
        Assert.Equal("dab", resource.Command);
    }

    [Fact]
    public async Task RunAsExecutable_DefaultConfig_HasCorrectArgs()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var containerBuilder = builder.AddDataAPIBuilder("dab");
        var executableBuilder = containerBuilder.RunAsExecutable();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderExecutableResource>());
        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("start", arg),
            arg => Assert.Equal("--config", arg),
            arg => Assert.Equal("dab-config.json", arg));
    }

    [Fact]
    public async Task RunAsExecutable_SingleConfigFile_HasCorrectArgs()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var containerBuilder = builder.AddDataAPIBuilder("dab", "./dab-config.json");
        var executableBuilder = containerBuilder.RunAsExecutable();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderExecutableResource>());
        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("start", arg),
            arg => Assert.Equal("--config", arg),
            arg => Assert.Equal("dab-config.json", arg));
    }

    [Fact]
    public async Task RunAsExecutable_MultipleConfigFiles_HasCorrectArgs()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var containerBuilder = builder.AddDataAPIBuilder("dab", "./dab-config.json", "./dab-config-2.json");
        var executableBuilder = containerBuilder.RunAsExecutable();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderExecutableResource>());
        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("start", arg),
            arg => Assert.Equal("--config", arg),
            arg => Assert.Equal("dab-config.json", arg),
            arg => Assert.Equal("dab-config-2.json", arg));
    }

    [Fact]
    public void RunAsExecutable_WithHttpPort_HasCorrectEndpoints()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var containerBuilder = builder.AddDataAPIBuilder("dab", httpPort: 1234, configFilePaths: "./dab-config.json");
        var executableBuilder = containerBuilder.RunAsExecutable();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderExecutableResource>());
        
        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpointAnnotations));
        Assert.Equal(2, endpointAnnotations.Count());

        var httpEndpoint = endpointAnnotations.FirstOrDefault(e => e.Name == DataApiBuilderExecutableResource.HttpEndpointName);
        Assert.NotNull(httpEndpoint);
        Assert.Equal(1234, httpEndpoint.Port);
        Assert.Equal(DataApiBuilderExecutableResource.HttpEndpointPort, httpEndpoint.TargetPort);

        var httpsEndpoint = endpointAnnotations.FirstOrDefault(e => e.Name == DataApiBuilderExecutableResource.HttpsEndpointName);
        Assert.NotNull(httpsEndpoint);
        Assert.Equal(DataApiBuilderExecutableResource.HttpsEndpointPort, httpsEndpoint.TargetPort);
    }

    [Fact]
    public void RunAsExecutable_HasCorrectEnvironmentVariables()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var containerBuilder = builder.AddDataAPIBuilder("dab", "./dab-config.json");
        var executableBuilder = containerBuilder.RunAsExecutable();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderExecutableResource>());

        Assert.True(resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var environmentAnnotations));
        
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));
        
        foreach (var annotation in environmentAnnotations)
        {
            annotation.Callback(context);
        }

        Assert.Contains("ASPNETCORE_URLS", context.EnvironmentVariables.Keys);
        Assert.Equal("http://localhost:5000;https://localhost:5001", context.EnvironmentVariables["ASPNETCORE_URLS"]);
    }

    [Fact]
    public void RunAsExecutable_WithCustomConfiguration_InvokesConfigureCallback()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        bool configureCallbackInvoked = false;

        var containerBuilder = builder.AddDataAPIBuilder("dab", "./dab-config.json");
        var executableBuilder = containerBuilder.RunAsExecutable(rb =>
        {
            configureCallbackInvoked = true;
            rb.WithEnvironment("TEST_VAR", "test_value");
        });

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<DataApiBuilderExecutableResource>());

        Assert.True(configureCallbackInvoked);

        Assert.True(resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var environmentAnnotations));
        
        var context = new EnvironmentCallbackContext(new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));
        
        foreach (var annotation in environmentAnnotations)
        {
            annotation.Callback(context);
        }

        Assert.Contains("TEST_VAR", context.EnvironmentVariables.Keys);
        Assert.Equal("test_value", context.EnvironmentVariables["TEST_VAR"]);
    }

    [Fact]
    public void RunAsExecutable_InvalidConfigFile_ThrowsFileNotFoundException()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();

        var nonExistentFileName = Guid.NewGuid().ToString();
        Assert.Throws<FileNotFoundException>(() =>
        {
            var containerBuilder = builder.AddDataAPIBuilder("dab", nonExistentFileName);
            containerBuilder.RunAsExecutable();
        });
    }

    [Fact]
    public void RunAsExecutable_WithoutConfigFilePaths_ThrowsInvalidOperationException()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        
        // Create a container resource manually without setting ConfigFilePaths
        var resource = new DataApiBuilderContainerResource("dab");
        var containerBuilder = builder.AddResource(resource);

        Assert.Throws<InvalidOperationException>(() => containerBuilder.RunAsExecutable());
    }
}

using Aspire.Hosting;
namespace CommunityToolkit.Aspire.Hosting.LlamaCpp.Tests;

public class AddLlamaServerTests
{
    const string modelUrl = "this.is/some-model-path/modelname.gguf";
    const string model2Url = "this.is/some-model-path/modelname2.gguf";
    const string mmprojUrl = "this.is/some-model-path/mmproj-modelname.gguf";
    [Fact]
    public void AddLlamaServer_WithModelUrl_SetsModelName()
    {
        var builder = DistributedApplication.CreateBuilder();
                
        var modelName = Path.GetFileName(modelUrl);
        var server = builder.AddLlamaServer("my-llama-server", modelUrl);

        var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<LlamaCppServerResource>());

        Assert.Equal("my-llama-server", resource.Name);
        Assert.Equal(modelName, resource.ModelName);
    }

    [Fact]
    public void AddLlamaServer_WithNullBuilder_ThrowsArgumentNullException()
    {
        IDistributedApplicationBuilder? builder = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.AddLlamaServer("test", modelUrl));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddLlamaServer_WithNullName_ThrowsArgumentNullException()
    {
        var builder = DistributedApplication.CreateBuilder();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.AddLlamaServer(null!, modelUrl));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void AddLlamaServer_WithNullModelUrl_ThrowsArgumentNullException()
    {
        var builder = DistributedApplication.CreateBuilder();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.AddLlamaServer("test", null!));

        Assert.Equal("modelUrl", exception.ParamName);
    }

    [Fact]
    public void AddLlamaServer_WithPort_ConfiguresHttpEndpoint()
    {
        var builder = DistributedApplication.CreateBuilder();
        const int port = 9000;

        var server = builder.AddLlamaServer("my-server", modelUrl, port);

        var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<LlamaCppServerResource>());

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().FirstOrDefault();
        Assert.NotNull(endpoint);
        Assert.Equal(port, endpoint.Port);
    }

    [Fact]
    public void AddLlamaServer_WithoutPort_UsesDefaultPort()
    {
        var builder = DistributedApplication.CreateBuilder();

        var server = builder.AddLlamaServer("my-server", modelUrl);

        var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<LlamaCppServerResource>());

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>().FirstOrDefault();
        Assert.NotNull(endpoint);
        Assert.Equal(8080, endpoint.TargetPort);
    }

    [Fact]
    public void AddLlamaServer_ConfiguresContainerImage()
    {
        var builder = DistributedApplication.CreateBuilder();

        var server = builder.AddLlamaServer("my-server", modelUrl);

        var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<LlamaCppServerResource>());

        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().FirstOrDefault();
        Assert.NotNull(imageAnnotation);
        Assert.NotEmpty(imageAnnotation.Image);
    }

    [Fact]
    public void AddLlamaServer_WithModelUrl_SetsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();       

        var server = builder.AddLlamaServer("my-server", modelUrl);

        Assert.Contains("LLAMA_ARG_MODEL_URL", server.Resource.EnvironmentArgs);
    }


    [Fact]
    public void WithReasoning_EnablesReasoning()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithReasoning(true);

        Assert.Contains("LLAMA_ARG_REASONING", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithReasoning_DisablesReasoning()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithReasoning(false);

        Assert.Contains("LLAMA_ARG_REASONING", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithReasoning_Default_EnablesReasoning()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithReasoning();

        Assert.Contains("LLAMA_ARG_REASONING", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithReasoning_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithReasoning(true);

        var exception = Assert.Throws<InvalidOperationException>(() => server.WithReasoning(false));

        Assert.Contains("Reasoning was already defined", exception.Message);
    }

    [Fact]
    public void WithReasoning_WithNullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<LlamaCppServerResource>? builder = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.WithReasoning());

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void WithApikeys_AddsApiKeys()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithApikeys("key1", "key2", "key3");

        Assert.Contains("LLAMA_API_KEY", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithApikeys_SingleKey()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithApikeys("single-key");

        Assert.Contains("LLAMA_API_KEY", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithApikeys_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithApikeys("key1");

        var exception = Assert.Throws<InvalidOperationException>(() => server.WithApikeys("key2"));

        Assert.Contains("already defined Api keys", exception.Message);
    }

    [Fact]
    public void WithApikeys_WithNullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<LlamaCppServerResource>? builder = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.WithApikeys("key"));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void WithApikeys_WithNullKeys_ThrowsArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var server = appBuilder.AddLlamaServer("my-server",     modelUrl);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            server.WithApikeys(null!));

        Assert.Equal("keys", exception.ParamName);
    }

    [Fact]
    public void WithContextSize_SetsContextSize()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithContextSize(2048);

        Assert.Contains("LLAMA_ARG_CTX_SIZE", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithContextSize_Default_SetsContextSizeToZero()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithContextSize();

        Assert.Contains("LLAMA_ARG_CTX_SIZE", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithContextSize_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithContextSize(2048);

        var exception = Assert.Throws<InvalidOperationException>(() => server.WithContextSize(4096));

        Assert.Contains("Context size was already defined", exception.Message);
    }

    [Fact]
    public void WithContextSize_WithNullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<LlamaCppServerResource>? builder = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.WithContextSize(2048));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void WithModelAlias_SetsAlias()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithModelAlias("my-model-alias");

        Assert.Contains("LLAMA_ARG_ALIAS", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithModelAlias_WithEmptyAlias_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        var exception = Assert.Throws<InvalidOperationException>(() => server.WithModelAlias(""));

        Assert.Contains("Alias cannot be empty", exception.Message);
    }

    [Fact]
    public void WithModelAlias_WithWhitespaceAlias_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        var exception = Assert.Throws<InvalidOperationException>(() => server.WithModelAlias("   "));

        Assert.Contains("Alias cannot be empty", exception.Message);
    }

    [Fact]
    public void WithModelAlias_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithModelAlias("alias1");

        var exception = Assert.Throws<InvalidOperationException>(() => server.WithModelAlias("alias2"));

        Assert.Contains("Model alias was already defined", exception.Message);
    }

    [Fact]
    public void WithModelAlias_WithNullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<LlamaCppServerResource>? builder = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.WithModelAlias("alias"));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void WithMultimodalProjection_ConfiguresProjectionFile()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server = server.WithMultimodalProjection(mmprojUrl);

        Assert.Contains("LLAMA_ARG_MMPROJ_URL", server.Resource.EnvironmentArgs);
        Assert.Contains("LLAMA_ARG_MMPROJ", server.Resource.EnvironmentArgs);
    }

    [Fact]
    public void WithMultimodalProjection_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server = server.WithMultimodalProjection(mmprojUrl);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            server.WithMultimodalProjection(mmprojUrl));

        Assert.Contains("Projection file url was already defined", exception.Message);
    }

    [Fact]
    public void WithMultimodalProjection_WithNullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<LlamaCppServerResource>? builder = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.WithMultimodalProjection(mmprojUrl));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void WithMultimodalProjection_WithNullUrl_ThrowsArgumentNullException()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var server = appBuilder.AddLlamaServer("my-server", modelUrl);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            server.WithMultimodalProjection(null!));

        Assert.Equal("projectionFileUrl", exception.ParamName);
    }

    [Fact]
    public void WithDataVolume_CreatesNewVolume()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithDataVolume("my-volume", isReadOnly: false);

        Assert.Equal("my-volume", server.Resource.VolumeName);
    }

    [Fact]
    public void WithDataVolume_WithoutName_GeneratesVolumeName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithDataVolume();

        Assert.NotNull(server.Resource.VolumeName);
        Assert.NotEmpty(server.Resource.VolumeName);
    }

    [Fact]
    public void WithDataVolume_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("my-server", modelUrl);

        server.WithDataVolume("volume1");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            server.WithDataVolume("volume2"));

        Assert.Contains("already has a data volume associated", exception.Message);
    }

    [Fact]
    public void WithDataVolume_WithNullBuilder_ThrowsArgumentNullException()
    {
        IResourceBuilder<LlamaCppServerResource>? builder = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder!.WithDataVolume("volume"));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void WithDataVolume_ShareVolume_SameBuilder_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddLlamaServer("server", modelUrl);

        server.WithDataVolume("volume1");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            server.WithDataVolume(server, isReadOnly: false));

        Assert.Contains("must be different", exception.Message);
    }

    [Fact]
    public void WithDataVolume_ShareVolume_WithSameModelName_SetsVolumeName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server1 = builder.AddLlamaServer("server1", modelUrl);
        var server2 = builder.AddLlamaServer("server2", modelUrl);

        server1.WithDataVolume("shared-volume");
        server2.WithDataVolume(server1, isReadOnly: false);

        Assert.Equal("shared-volume", server2.Resource.VolumeName);
    }

    [Fact]
    public void WithDataVolume_ShareVolume_WithDifferentModelName_SetsVolumeName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server1 = builder.AddLlamaServer("server1", modelUrl);
        var server2 = builder.AddLlamaServer("server2",model2Url);

        server1.WithDataVolume("shared-volume");
        server2.WithDataVolume(server1, isReadOnly: false);

        Assert.Equal("shared-volume", server2.Resource.VolumeName);
    }
}

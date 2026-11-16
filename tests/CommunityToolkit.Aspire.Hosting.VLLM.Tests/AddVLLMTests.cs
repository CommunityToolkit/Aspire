using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.VLLM.Tests;

public class AddVLLMTests
{
    [Fact]
    public void AddVLLMRegistersResourceWithDefaultImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm");

        using var app = builder.Build();

        var resource = AssertSingleResource(app);
        Assert.Equal("vllm", resource.Name);

        Assert.True(resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image));
        Assert.Equal(VLLMContainerImageTags.Registry, image.Registry);
        Assert.Equal(VLLMContainerImageTags.Image, image.Image);
        Assert.Equal(VLLMContainerImageTags.Tag, image.Tag);
    }

    [Fact]
    public async Task AddVLLMSetsSharedMemorySize()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm");

        using var app = builder.Build();

        await AssertContainerRuntimeArgs(AssertSingleResource(app));
    }

    [Fact]
    public void AddVLLMConfiguresHttpEndpointAndHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm");

        using var app = builder.Build();

        var resource = AssertSingleResource(app);
        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal("http", endpoint.Name);
        Assert.Equal("http", endpoint.UriScheme);
        Assert.Equal(8000, endpoint.TargetPort);
        Assert.Null(endpoint.Port);

        Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>());
    }

    [Fact]
    public void AddVLLMWithExplicitPortSetsHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm", port: 9000);

        using var app = builder.Build();

        var resource = AssertSingleResource(app);
        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(9000, endpoint.Port);
        Assert.Equal(8000, endpoint.TargetPort);
    }

    [Fact]
    public async Task ConnectionStringUsesEndpointFormat()
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm");
        vllm.WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27000));

        var connectionString = await vllm.Resource.ConnectionStringExpression.GetValueAsync(default);

        Assert.Equal("Endpoint=http://localhost:27000", connectionString);
        Assert.Equal("Endpoint={vllm.bindings.http.scheme}://{vllm.bindings.http.host}:{vllm.bindings.http.port}", vllm.Resource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public void ConnectionPropertiesIncludeHostPortAndUri()
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm");

        var properties = ((IResourceWithConnectionString)vllm.Resource).GetConnectionProperties().ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal(["Host", "Port", "Uri"], properties.Keys);
        Assert.Equal("{vllm.bindings.http.scheme}://{vllm.bindings.http.host}:{vllm.bindings.http.port}", properties["Uri"].ValueExpression);
    }

    [Fact]
    public async Task WithNvidiaGPUSupport()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm").WithGPUSupport();

        using var app = builder.Build();

        await AssertContainerRuntimeArgs(AssertSingleResource(app), "--gpus", "all");
    }

    [Theory]
    [InlineData("ASPIRE_CONTAINER_RUNTIME")]
    [InlineData("DOTNET_ASPIRE_CONTAINER_RUNTIME")]
    public async Task WithNvidiaGPUSupportOnPodman(string configKey)
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration[configKey] = "podman";
        builder.AddVLLM("vllm").WithGPUSupport(VLLMGpuVendor.Nvidia);

        using var app = builder.Build();

        await AssertContainerRuntimeArgs(AssertSingleResource(app), "--device", "nvidia.com/gpu=all");
    }

    [Fact]
    public async Task WithAMDGPUSupportSwitchesToRocmImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm").WithGPUSupport(VLLMGpuVendor.AMD);

        using var app = builder.Build();

        var resource = AssertSingleResource(app);
        await AssertContainerRuntimeArgs(resource, "--device", "/dev/kfd", "--device", "/dev/dri");

        Assert.True(resource.TryGetLastAnnotation<ContainerImageAnnotation>(out var image));
        Assert.Equal(VLLMContainerImageTags.RocmImage, image.Image);
        Assert.Equal(VLLMContainerImageTags.Tag, image.Tag);
    }

    [Fact]
    public void WithAMDGPUSupportKeepsCustomImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm").WithImage("my/vllm", "dev").WithGPUSupport(VLLMGpuVendor.AMD);

        using var app = builder.Build();

        Assert.True(AssertSingleResource(app).TryGetLastAnnotation<ContainerImageAnnotation>(out var image));
        Assert.Equal("my/vllm", image.Image);
        Assert.Equal("dev", image.Tag);
    }

    [Fact]
    public async Task WithModelIsFirstArgumentRegardlessOfOrder()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm")
            .WithArgs("--dtype", "bfloat16")
            .WithServedModelName("my-model")
            .WithModel("Qwen/Qwen3-0.6B");

        using var app = builder.Build();

        var args = await ArgumentEvaluator.GetArgumentListAsync(AssertSingleResource(app));

        Assert.Equal(["Qwen/Qwen3-0.6B", "--dtype", "bfloat16", "--served-model-name", "my-model"], args);
    }

    [Fact]
    public async Task WithModelReplacesPreviousValue()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddVLLM("vllm")
            .WithModel("model-a")
            .WithArgs("--dtype", "bfloat16")
            .WithModel("model-b");

        using var app = builder.Build();

        var args = await ArgumentEvaluator.GetArgumentListAsync(AssertSingleResource(app));

        Assert.Equal(["model-b", "--dtype", "bfloat16"], args);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hf-cache")]
    public void WithDataVolumeMountsHuggingFaceCache(string? volumeName)
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm").WithDataVolume(volumeName);

        using var app = builder.Build();

        var mount = Assert.Single(AssertSingleResource(app).Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(volumeName ?? VolumeNameGenerator.Generate(vllm, "huggingface"), mount.Source);
        Assert.Equal("/root/.cache/huggingface", mount.Target);
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.False(mount.IsReadOnly);
    }

    [Fact]
    public async Task WithHuggingFaceTokenSetsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["Parameters:hf-token"] = "secret-token";
        var token = builder.AddParameter("hf-token", secret: true);
        builder.AddVLLM("vllm").WithHuggingFaceToken(token);

        using var app = builder.Build();

        var env = await EnvironmentVariableEvaluator.GetEnvironmentVariablesAsync(AssertSingleResource(app));

        Assert.Equal("secret-token", env["HF_TOKEN"]);
    }

    private static VLLMResource AssertSingleResource(DistributedApplication app)
    {
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        return Assert.Single(appModel.Resources.OfType<VLLMResource>());
    }

    private static async Task AssertContainerRuntimeArgs(IResource resource, params string[] expectedArgs)
    {
        var args = new List<object>();
        var context = new ContainerRuntimeArgsCallbackContext(args, CancellationToken.None);
        foreach (var annotation in resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
        {
            await annotation.Callback(context);
        }

        Assert.Equal(["--shm-size", "8g", .. expectedArgs], args.Select(a => a.ToString()));
    }
}

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data.Common;

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
    public async Task AddModelIsFirstArgumentRegardlessOfOrder()
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm")
            .WithArgs("--dtype", "bfloat16")
            .WithServedModelName("my-model");
        var model = vllm.AddModel("qwen", "Qwen/Qwen3-0.6B");

        using var app = builder.Build();

        var args = await ArgumentEvaluator.GetArgumentListAsync(AssertSingleResource(app));

        Assert.Equal(["Qwen/Qwen3-0.6B", "--dtype", "bfloat16", "--served-model-name", "my-model"], args);
        Assert.Same(vllm.Resource, model.Resource.Parent);
    }

    [Theory]
    [InlineData("Qwen/Qwen3-0.6B", "vllm-Qwen3-0-6B")]
    [InlineData("org/model:tag", "vllm-model")]
    [InlineData("/models/my_model/", "vllm-my-model")]
    public void AddModelGeneratesResourceName(string modelId, string expectedName)
    {
        var builder = DistributedApplication.CreateBuilder();
        var model = builder.AddVLLM("vllm").AddModel(modelId);

        Assert.Equal(expectedName, model.Resource.Name);
        Assert.Equal(modelId, model.Resource.ModelName);
    }

    [Fact]
    public async Task AddSecondModelFailsWithoutChangingTheFirstModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm");
        vllm.AddModel("first", "model-a");

        var exception = Assert.Throws<DistributedApplicationException>(() => vllm.AddModel("second", "model-b"));

        Assert.Contains("another AddVLLM resource", exception.Message);
        Assert.Equal(["model-a"], await vllm.Resource.GetArgumentListAsync());
        Assert.Single(builder.Resources.OfType<VLLMModelResource>());
    }

    [Theory]
    [InlineData(DistributedApplicationOperation.Run)]
    [InlineData(DistributedApplicationOperation.Publish)]
    public async Task ServerWithoutModelFailsWhenArgumentsAreEvaluated(DistributedApplicationOperation operation)
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm");
        var executionContext = new DistributedApplicationExecutionContext(
            new DistributedApplicationExecutionContextOptions(operation));

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(async () =>
            await ExecutionConfigurationBuilder.Create(vllm.Resource)
                .WithArgumentsConfig()
                .BuildAsync(executionContext, NullLogger.Instance, TestContext.Current.CancellationToken));
        Assert.Contains("'vllm' requires a model", exception.Message);
        Assert.Contains("AddModel", exception.Message);
    }

    [Theory]
    [InlineData("Qwen/Qwen3-0.6B")]
    [InlineData("/models/model;revision=1")]
    public async Task ModelConnectionStringContainsEscapedModelAndParentEndpoint(string modelId)
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm");
        vllm.WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27000));
        var model = vllm.AddModel("model", modelId);

        var connectionString = await model.Resource.ConnectionStringExpression.GetValueAsync(default);
        var connection = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var properties = ((IResourceWithConnectionString)model.Resource).GetConnectionProperties().ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal("http://localhost:27000", connection["Endpoint"]);
        Assert.Equal(modelId, connection["Model"]);
        Assert.Equal(["Host", "Port", "Uri", "Model"], properties.Keys);
        Assert.Equal(modelId, await properties["Model"].GetValueAsync(default));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ServedModelAliasIsUsedInArgumentsAndModelConnectionRegardlessOfOrder(bool aliasBeforeModel)
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm");
        vllm.WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27000));

        if (aliasBeforeModel)
        {
            vllm.WithServedModelName("old-alias");
        }

        var model = vllm.AddModel("model", "org/model");
        var consumer = builder.AddExecutable("consumer", "dotnet", ".").WithReference(model);
        if (!aliasBeforeModel)
        {
            vllm.WithServedModelName("old-alias");
        }

        vllm.WithServedModelName("new-alias");

        var connectionString = await model.Resource.ConnectionStringExpression.GetValueAsync(default);
        var connection = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var environment = await consumer.Resource.GetEnvironmentVariablesAsync();
        var referencedConnection = new DbConnectionStringBuilder { ConnectionString = environment["ConnectionStrings__model"] };
        var properties = ((IResourceWithConnectionString)model.Resource).GetConnectionProperties().ToDictionary(p => p.Key, p => p.Value);

        Assert.Equal("new-alias", connection["Model"]);
        Assert.Equal("new-alias", referencedConnection["Model"]);
        Assert.Equal("new-alias", await properties["Model"].GetValueAsync(default));
        Assert.Equal(["org/model", "--served-model-name", "new-alias"], await vllm.Resource.GetArgumentListAsync());
    }

    [Fact]
    public async Task ReferencingAndWaitingForModelUsesConnectionAndParentHealth()
    {
        var builder = DistributedApplication.CreateBuilder();
        var vllm = builder.AddVLLM("vllm");
        vllm.WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27000));
        var model = vllm.AddModel("qwen", "Qwen/Qwen3-0.6B");
        var consumer = builder.AddExecutable("consumer", "dotnet", ".")
            .WithReference(model)
            .WaitFor(model);

        var environment = await consumer.Resource.GetEnvironmentVariablesAsync();
        var connection = new DbConnectionStringBuilder { ConnectionString = environment["ConnectionStrings__qwen"] };
        var waits = consumer.Resource.Annotations.OfType<WaitAnnotation>();

        Assert.Equal("http://localhost:27000", connection["Endpoint"]);
        Assert.Equal("Qwen/Qwen3-0.6B", connection["Model"]);
        Assert.IsAssignableFrom<IResourceWithoutLifetime>(model.Resource);
        Assert.Contains(waits, wait => wait.Resource == vllm.Resource && wait.WaitType == WaitType.WaitUntilHealthy);
        Assert.Contains(waits, wait => wait.Resource == model.Resource && wait.WaitType == WaitType.WaitUntilHealthy);
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

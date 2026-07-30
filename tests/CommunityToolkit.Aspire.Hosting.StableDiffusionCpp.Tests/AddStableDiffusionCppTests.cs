// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.StableDiffusionCpp.Tests;

public class AddStableDiffusionCppTests
{
    [Theory]
    [InlineData(StableDiffusionCppImageVariant.Cuda, "master-cuda")]
    [InlineData(StableDiffusionCppImageVariant.CudaSpark, "master-cuda-spark")]
    [InlineData(StableDiffusionCppImageVariant.Vulkan, "master-vulkan")]
    [InlineData(StableDiffusionCppImageVariant.Sycl, "master-sycl")]
    [InlineData(StableDiffusionCppImageVariant.Musa, "master-musa")]
    public void ResourceUsesSelectedOfficialImage(
        StableDiffusionCppImageVariant variant,
        string expectedTag)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStableDiffusionCpp("stable-diffusion", variant);

        using var app = builder.Build();
        var resource = GetResource(app);
        var image = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("ghcr.io", image.Registry);
        Assert.Equal("leejet/stable-diffusion.cpp", image.Image);
        Assert.Equal(expectedTag, image.Tag);
        Assert.Equal(variant, resource.ImageVariant);
    }

    [Fact]
    public void ResourceHasHttpEndpointAndHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStableDiffusionCpp("stable-diffusion", port: 5678);

        using var app = builder.Build();
        var resource = GetResource(app);
        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());

        Assert.Equal("http", endpoint.Name);
        Assert.Equal(5678, endpoint.Port);
        Assert.Equal(1234, endpoint.TargetPort);
        Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>());
    }

    [Fact]
    public void ResourceUsesServerEntrypointAndRequiredDirectories()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddStableDiffusionCpp("stable-diffusion");

        using var app = builder.Build();
        var resource = GetResource(app);

        var mounts = resource.Annotations.OfType<ContainerMountAnnotation>().ToList();
        Assert.Contains(mounts, mount => mount.Target == "/models");
        Assert.Contains(mounts, mount => mount.Target == "/output");

        var args = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToList();
        Assert.NotEmpty(args);
    }

    [Fact]
    public void HuggingFaceModelRegistersChildResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddStableDiffusionCpp("stable-diffusion");

        var modelBuilder = server.AddHuggingFaceModel(
            "stabilityai/stable-diffusion-xl-base-1.0",
            "sd_xl_base_1.0.safetensors",
            "revision");

        using var app = builder.Build();
        var model = Assert.Single(
            app.Services.GetRequiredService<DistributedApplicationModel>()
                .Resources.OfType<StableDiffusionCppModelResource>());

        Assert.Same(modelBuilder.Resource, model);
        Assert.Same(server.Resource, model.Parent);
        Assert.Same(model, server.Resource.Model);
        Assert.Equal("stabilityai/stable-diffusion-xl-base-1.0", model.Repository);
        Assert.Equal("sd_xl_base_1.0.safetensors", model.FileName);
        Assert.Equal("revision", model.Revision);
        Assert.Contains(
            model.Annotations.OfType<ResourceRelationshipAnnotation>(),
            annotation => annotation.Resource == server.Resource && annotation.Type == "Parent");
    }

    [Fact]
    public void OnlyOneActiveModelCanBeAdded()
    {
        var builder = DistributedApplication.CreateBuilder();
        var server = builder.AddStableDiffusionCpp("stable-diffusion");
        server.AddHuggingFaceModel("organization/model", "first.safetensors");

        var action = () =>
            server.AddHuggingFaceModel("organization/model", "second.safetensors");

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void HuggingFaceTokenIsStoredAsParameterResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var token = builder.AddParameter("hugging-face-token", secret: true);
        var server = builder.AddStableDiffusionCpp("stable-diffusion")
            .WithHuggingFaceToken(token);

        Assert.Same(token.Resource, server.Resource.HuggingFaceToken);
    }

    [Theory]
    [InlineData(StableDiffusionCppImageVariant.Cuda, "--gpus", "all")]
    [InlineData(StableDiffusionCppImageVariant.CudaSpark, "--gpus", "all")]
    [InlineData(StableDiffusionCppImageVariant.Vulkan, "--device", "/dev/dri")]
    [InlineData(StableDiffusionCppImageVariant.Sycl, "--device", "/dev/dri")]
    public async Task GpuSupportUsesVariantRuntimeArguments(
        StableDiffusionCppImageVariant variant,
        string firstArgument,
        string secondArgument)
    {
        var builder = DistributedApplication.CreateBuilder();
        var resourceBuilder = builder.AddStableDiffusionCpp("stable-diffusion", variant)
            .WithGPUSupport();

        using var app = builder.Build();
        var annotation = Assert.Single(
            resourceBuilder.Resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>());
        var context = new ContainerRuntimeArgsCallbackContext([]);

        await annotation.Callback(context);

        Assert.Equal([firstArgument, secondArgument], context.Args);
    }

    [Fact]
    public async Task NvidiaGpuSupportUsesPodmanDevice()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["ASPIRE_CONTAINER_RUNTIME"] = "podman";
        var resourceBuilder = builder
            .AddStableDiffusionCpp("stable-diffusion", StableDiffusionCppImageVariant.Cuda)
            .WithGPUSupport();

        using var app = builder.Build();
        var annotation = Assert.Single(
            resourceBuilder.Resource.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>());
        var context = new ContainerRuntimeArgsCallbackContext([]);

        await annotation.Callback(context);

        Assert.Equal(["--device", "nvidia.com/gpu=all"], context.Args);
    }

    [Fact]
    public void DownloadUriEscapesRepositoryRevisionAndFilePath()
    {
        var uri = HuggingFaceModelDownloader.BuildDownloadUri(
            "organization/model",
            "feature branch",
            "checkpoints/model file.safetensors");

        Assert.Equal(
            "https://huggingface.co/organization/model/resolve/feature%20branch/checkpoints/model%20file.safetensors",
            uri.AbsoluteUri);
    }

    [Fact]
    public void TargetPathRejectsTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "stable-diffusion-cpp-models");

        var action = () => HuggingFaceModelDownloader.GetTargetPath(root, "../secret.txt");

        Assert.Throws<InvalidOperationException>(action);
    }

    [Fact]
    public void PublicMethodsValidateArguments()
    {
        IDistributedApplicationBuilder nullApplicationBuilder = null!;
        IResourceBuilder<StableDiffusionCppResource> nullResourceBuilder = null!;

        Assert.Throws<ArgumentNullException>(
            () => nullApplicationBuilder.AddStableDiffusionCpp("stable-diffusion"));
        Assert.Throws<ArgumentNullException>(
            () => nullResourceBuilder.WithGPUSupport());
        Assert.Throws<ArgumentNullException>(
            () => nullResourceBuilder.WithVaeTiling());
        Assert.Throws<ArgumentNullException>(
            () => nullResourceBuilder.AddHuggingFaceModel("organization/model", "model.safetensors"));
    }

    private static StableDiffusionCppResource GetResource(DistributedApplication app) =>
        Assert.Single(
            app.Services.GetRequiredService<DistributedApplicationModel>()
                .Resources.OfType<StableDiffusionCppResource>());
}
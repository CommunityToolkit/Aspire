// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Pipeline APIs are experimental
#pragma warning disable ASPIREPIPELINES003 // ContainerBuildOptionsCallbackAnnotation is experimental
#pragma warning disable ASPIRECONTAINERRUNTIME001 // IContainerRuntimeResolver is experimental

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindImageLoadingTests
{
    [Fact]
    public async Task DoesNotLoadPublicRegistryImages()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner();

        builder.AddKubernetesEnvironment("k8s").WithKind();
        builder.AddContainer("redis", "redis", "7");

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommands = fakeRunner.Commands
            .Where(c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"))
            .ToList();

        // Public registry images are pulled by Kind nodes directly, not preloaded
        Assert.Empty(kindLoadCommands);
    }

    [Fact]
    public async Task LoadsProjectResourceImageUsingBuildOptionsCallback()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner();

        builder.AddKubernetesEnvironment("k8s").WithKind();

        // Simulate a project resource by adding ContainerBuildOptionsCallbackAnnotation
        // with default image name, just like ProjectResource's constructor does.
        var apiBuilder = builder.AddContainer("api", "placeholder", "placeholder");
        apiBuilder.Resource.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(ctx =>
        {
            ctx.LocalImageName = "api";
            ctx.LocalImageTag = "latest";
        }));

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommands = fakeRunner.Commands
            .Where(c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"))
            .ToList();

        // CBOCA image name is used for loading
        Assert.Single(kindLoadCommands);
        Assert.Contains("api:latest", kindLoadCommands[0].Arguments);
    }

    [Fact]
    public async Task LoadsImageUsingBuildOptionsWhenNoContainerImageAnnotation()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner();

        builder.AddKubernetesEnvironment("k8s").WithKind();

        // Create a resource with only ContainerBuildOptionsCallbackAnnotation
        // and no ContainerImageAnnotation (like a real ProjectResource).
        var resource = new TestComputeResource("myservice");
        resource.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(ctx =>
        {
            ctx.LocalImageName = "myservice";
            ctx.LocalImageTag = "v1.0";
        }));
        builder.AddResource(resource);

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommands = fakeRunner.Commands
            .Where(c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"))
            .ToList();

        Assert.Single(kindLoadCommands);
        Assert.Contains("myservice:v1.0", kindLoadCommands[0].Arguments);
    }

    [Fact]
    public async Task LoadsCustomizedProjectImageName()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner();

        builder.AddKubernetesEnvironment("k8s").WithKind();

        // Simulate a project resource with a customized image name via
        // WithContainerBuildOptions (user override).
        var resource = new TestComputeResource("api");
        resource.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(ctx =>
        {
            ctx.LocalImageName = "myregistry.io/myteam/api";
            ctx.LocalImageTag = "v2.1.0";
        }));
        builder.AddResource(resource);

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommands = fakeRunner.Commands
            .Where(c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"))
            .ToList();

        Assert.Single(kindLoadCommands);
        Assert.Contains("myregistry.io/myteam/api:v2.1.0", kindLoadCommands[0].Arguments);
    }

    [Fact]
    public async Task LoadsOnlyLocallyBuiltImages()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner();

        builder.AddKubernetesEnvironment("k8s").WithKind();
        builder.AddContainer("redis", "redis", "7");
        builder.AddContainer("nginx", "nginx", "latest");

        var apiResource = new TestComputeResource("api");
        apiResource.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(ctx =>
        {
            ctx.LocalImageName = "api";
            ctx.LocalImageTag = "latest";
        }));
        builder.AddResource(apiResource);

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommands = fakeRunner.Commands
            .Where(c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"))
            .ToList();

        // Only the locally-built api image should be loaded, not redis or nginx
        Assert.Single(kindLoadCommands);
        Assert.Contains("api:latest", kindLoadCommands[0].Arguments);
    }

    [Fact]
    public async Task SkipsResourcesWithoutBuildOptions()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner();

        builder.AddKubernetesEnvironment("k8s").WithKind();

        // A resource with no CBOCA - should not be loaded
        builder.AddResource(new TestComputeResource("noimage"));

        // A resource WITH CBOCA - should be loaded
        var api = new TestComputeResource("api");
        api.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(ctx =>
        {
            ctx.LocalImageName = "api";
            ctx.LocalImageTag = "latest";
        }));
        builder.AddResource(api);

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommands = fakeRunner.Commands
            .Where(c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"))
            .ToList();

        Assert.Single(kindLoadCommands);
        Assert.Contains("api:latest", kindLoadCommands[0].Arguments);
    }

    [Fact]
    public async Task UsesCorrectClusterName()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner();

        builder.AddKubernetesEnvironment("my-env").WithKind();

        var api = new TestComputeResource("api");
        api.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(ctx =>
        {
            ctx.LocalImageName = "api";
            ctx.LocalImageTag = "latest";
        }));
        builder.AddResource(api);

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommands = fakeRunner.Commands
            .Where(c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"))
            .ToList();

        Assert.Single(kindLoadCommands);
        Assert.Contains("--name my-env-kind", kindLoadCommands[0].Arguments);
    }

    [Fact]
    public async Task UsesPodmanProviderWhenLoadingImagesWithPodmanRuntime()
    {
        var (fakeRunner, builder) = CreateBuilderWithFakeRunner("ASPIRE_CONTAINER_RUNTIME=podman");

        builder.AddKubernetesEnvironment("k8s").WithKind();

        var api = new TestComputeResource("api");
        api.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(ctx =>
        {
            ctx.LocalImageName = "api";
            ctx.LocalImageTag = "latest";
        }));
        builder.AddResource(api);

        using var app = builder.Build();
        await app.RunAsync();

        var kindLoadCommand = Assert.Single(fakeRunner.Commands,
            c => c.FileName == "kind" && c.Arguments.Contains("load docker-image"));

        Assert.NotNull(kindLoadCommand.EnvironmentVariables);
        Assert.Equal("podman", kindLoadCommand.EnvironmentVariables["KIND_EXPERIMENTAL_PROVIDER"]);
    }

    private static (FakeProcessRunner runner, IDistributedApplicationTestingBuilder builder) CreateBuilderWithFakeRunner(params string[] args)
    {
        var fakeRunner = new FakeProcessRunner();
        var builderArgs = new List<string>
        {
            "AppHost:Operation=publish",
            $"Pipeline:OutputPath={Directory.CreateTempSubdirectory(".kind-test").FullName}",
            "Pipeline:Deploy=true",
        };
        builderArgs.AddRange(args);

        var builder = TestDistributedApplicationBuilder.Create(builderArgs.ToArray());

        builder.Services.AddSingleton<IProcessRunner>(fakeRunner);
        builder.Services.AddSingleton<global::Aspire.Hosting.Publishing.IContainerRuntimeResolver>(
            new FakeContainerRuntimeResolver(GetRuntimeName(args)));

        return (fakeRunner, builder);
    }

    private static string GetRuntimeName(params string[] args)
    {
        return args.Any(arg => string.Equals(arg, "ASPIRE_CONTAINER_RUNTIME=podman", StringComparison.OrdinalIgnoreCase))
            ? "Podman"
            : "Docker";
    }

    /// <summary>
    /// A minimal resource for testing image loading behavior without requiring
    /// a real project reference or container image annotation.
    /// </summary>
    private sealed class TestComputeResource(string name) : Resource(name);
}

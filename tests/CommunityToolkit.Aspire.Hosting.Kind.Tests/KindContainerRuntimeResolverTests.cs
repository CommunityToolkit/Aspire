// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindContainerRuntimeResolverTests
{
    [Fact]
    public async Task MapsPodmanRuntimeToKindProviderEnvironment()
    {
        var resolver = CreateResolver("Podman");

        var runtime = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal("podman", runtime.Executable);
        Assert.NotNull(runtime.KindEnvironmentVariables);
        Assert.Equal("podman", runtime.KindEnvironmentVariables["KIND_EXPERIMENTAL_PROVIDER"]);
    }

    [Fact]
    public async Task MapsDockerRuntimeToDockerExecutable()
    {
        var resolver = CreateResolver("Docker");

        var runtime = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal("docker", runtime.Executable);
        Assert.Null(runtime.KindEnvironmentVariables);
    }

    [Fact]
    public async Task RuntimeNameMatchingIsCaseInsensitive()
    {
        var resolver = CreateResolver("PODMAN");

        var runtime = await resolver.ResolveAsync(CancellationToken.None);

        Assert.Equal("podman", runtime.Executable);
    }

    [Fact]
    public async Task RejectsUnsupportedResolvedRuntime()
    {
        var resolver = CreateResolver("containerd");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(CancellationToken.None));

        Assert.Contains("containerd", exception.Message);
    }

    private static KindContainerRuntimeResolver CreateResolver(string runtimeName)
    {
        return new KindContainerRuntimeResolver(new FakeContainerRuntimeResolver(runtimeName));
    }
}

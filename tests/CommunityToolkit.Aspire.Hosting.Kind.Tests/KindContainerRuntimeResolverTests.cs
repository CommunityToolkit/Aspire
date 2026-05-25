// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindContainerRuntimeResolverTests
{
    [Fact]
    public async Task UsesConfiguredPodmanRuntime()
    {
        var runner = new FakeProcessRunner();
        var resolver = CreateResolver(runner, ("ASPIRE_CONTAINER_RUNTIME", "podman"));

        var runtime = await resolver.ResolveAsync(NullLogger.Instance, CancellationToken.None);

        Assert.Equal("podman", runtime.Executable);
        Assert.NotNull(runtime.KindEnvironmentVariables);
        Assert.Equal("podman", runtime.KindEnvironmentVariables["KIND_EXPERIMENTAL_PROVIDER"]);
        var command = Assert.Single(runner.Commands);
        Assert.Equal("podman", command.FileName);
        Assert.Equal("container ls -n 1", command.Arguments);
    }

    [Fact]
    public async Task UsesLegacyConfiguredRuntime()
    {
        var runner = new FakeProcessRunner();
        var resolver = CreateResolver(runner, ("DOTNET_ASPIRE_CONTAINER_RUNTIME", "podman"));

        var runtime = await resolver.ResolveAsync(NullLogger.Instance, CancellationToken.None);

        Assert.Equal("podman", runtime.Executable);
    }

    [Fact]
    public async Task RejectsUnsupportedConfiguredRuntime()
    {
        var runner = new FakeProcessRunner();
        var resolver = CreateResolver(runner, ("ASPIRE_CONTAINER_RUNTIME", "containerd"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(NullLogger.Instance, CancellationToken.None));

        Assert.Contains("containerd", exception.Message);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task PrefersDockerWhenNoRuntimeIsConfigured()
    {
        var runner = new FakeProcessRunner();
        var resolver = CreateResolver(runner);

        var runtime = await resolver.ResolveAsync(NullLogger.Instance, CancellationToken.None);

        Assert.Equal("docker", runtime.Executable);
        Assert.Null(runtime.KindEnvironmentVariables);
        Assert.Equal(["docker", "podman"], runner.Commands.Select(c => c.FileName).Order());
    }

    [Fact]
    public async Task UsesPodmanWhenDockerIsUnavailableAndPodmanIsAvailable()
    {
        var runner = new FakeProcessRunner();
        runner.ResultsByFileName["docker"] = new ProcessResult(1, "", "docker unavailable");
        runner.ResultsByFileName["podman"] = new ProcessResult(0, "", "");
        var resolver = CreateResolver(runner);

        var runtime = await resolver.ResolveAsync(NullLogger.Instance, CancellationToken.None);

        Assert.Equal("podman", runtime.Executable);
        Assert.Equal(["docker", "podman"], runner.Commands.Select(c => c.FileName).Order());
    }

    [Fact]
    public async Task ThrowsWhenConfiguredRuntimeIsUnavailable()
    {
        var runner = new FakeProcessRunner
        {
            NextResult = new ProcessResult(1, "", "podman unavailable")
        };
        var resolver = CreateResolver(runner, ("ASPIRE_CONTAINER_RUNTIME", "podman"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(NullLogger.Instance, CancellationToken.None));

        Assert.Contains("podman unavailable", exception.Message);
    }

    private static KindContainerRuntimeResolver CreateResolver(
        FakeProcessRunner runner,
        params (string Key, string Value)[] configuration)
    {
        var values = configuration.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value);
        return new KindContainerRuntimeResolver(
            runner,
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }
}

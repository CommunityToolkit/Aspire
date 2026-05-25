// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECONTAINERRUNTIME001 // IContainerRuntimeResolver is experimental
#pragma warning disable ASPIREPIPELINES003 // ContainerImageBuildOptions is experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

internal sealed class FakeContainerRuntimeResolver(string runtimeName) : IContainerRuntimeResolver
{
    private readonly FakeContainerRuntime _containerRuntime = new(runtimeName);

    public Task<IContainerRuntime> ResolveAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IContainerRuntime>(_containerRuntime);
    }

    private sealed class FakeContainerRuntime(string name) : IContainerRuntime
    {
        public string Name { get; } = name;

        public Task<bool> CheckIfRunningAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task BuildImageAsync(
            string contextPath,
            string dockerfilePath,
            ContainerImageBuildOptions? options,
            Dictionary<string, string?> buildArguments,
            Dictionary<string, BuildImageSecretValue> buildSecrets,
            string? stage,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task TagImageAsync(string localImageName, string targetImageName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task RemoveImageAsync(string imageName, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task PushImageAsync(IResource resource, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task LoginToRegistryAsync(string registryServer, string username, string password, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ComposeUpAsync(ComposeOperationContext context, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ComposeDownAsync(ComposeOperationContext context, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ComposeServiceInfo>?> ComposeListServicesAsync(ComposeOperationContext context, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}

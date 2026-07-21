// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Publishing;
namespace CommunityToolkit.Aspire.Hosting.Kind;

#pragma warning disable ASPIRECONTAINERRUNTIME001 // IContainerRuntimeResolver is experimental

internal sealed record KindContainerRuntime(string Executable)
{
    // kind selects Podman via KIND_EXPERIMENTAL_PROVIDER; Aspire's container runtime resolver only tells us which runtime Aspire selected.
    private static readonly IReadOnlyDictionary<string, string> PodmanKindEnvironmentVariables =
        new Dictionary<string, string>
        {
            ["KIND_EXPERIMENTAL_PROVIDER"] = "podman"
        };

    public IReadOnlyDictionary<string, string>? KindEnvironmentVariables =>
        Executable.Equals("podman", StringComparison.OrdinalIgnoreCase)
            ? PodmanKindEnvironmentVariables
            : null;
}

internal interface IKindContainerRuntimeResolver
{
    Task<KindContainerRuntime> ResolveAsync(CancellationToken cancellationToken);
}

internal sealed class KindContainerRuntimeResolver(IContainerRuntimeResolver containerRuntimeResolver) : IKindContainerRuntimeResolver
{
    private const string Docker = "docker";
    private const string Podman = "podman";

    public async Task<KindContainerRuntime> ResolveAsync(CancellationToken cancellationToken)
    {
        var containerRuntime = await containerRuntimeResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);

        if (IsRuntime(containerRuntime, Docker))
        {
            return new KindContainerRuntime(Docker);
        }

        if (IsRuntime(containerRuntime, Podman))
        {
            return new KindContainerRuntime(Podman);
        }

        throw new InvalidOperationException(
            $"Kind integration supports Aspire container runtimes '{Docker}' and '{Podman}', but Aspire resolved '{containerRuntime.Name}'.");
    }

    private static bool IsRuntime(IContainerRuntime containerRuntime, string runtimeName)
    {
        return string.Equals(containerRuntime.Name, runtimeName, StringComparison.OrdinalIgnoreCase);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Kind;

internal sealed record KindContainerRuntime(string Executable)
{
    private static readonly IReadOnlyDictionary<string, string?> PodmanKindEnvironmentVariables =
        new Dictionary<string, string?>
        {
            ["KIND_EXPERIMENTAL_PROVIDER"] = "podman"
        };

    public IReadOnlyDictionary<string, string?>? KindEnvironmentVariables =>
        Executable.Equals("podman", StringComparison.OrdinalIgnoreCase)
            ? PodmanKindEnvironmentVariables
            : null;
}

internal interface IKindContainerRuntimeResolver
{
    Task<KindContainerRuntime> ResolveAsync(ILogger logger, CancellationToken cancellationToken);
}

internal sealed class KindContainerRuntimeResolver(
    IProcessRunner processRunner,
    IConfiguration configuration) : IKindContainerRuntimeResolver
{
    private const string Docker = "docker";
    private const string Podman = "podman";
    private const string AspireContainerRuntimeConfigKey = "ASPIRE_CONTAINER_RUNTIME";
    private const string LegacyAspireContainerRuntimeConfigKey = "DOTNET_ASPIRE_CONTAINER_RUNTIME";

    public async Task<KindContainerRuntime> ResolveAsync(ILogger logger, CancellationToken cancellationToken)
    {
        var configuredRuntime = (configuration[AspireContainerRuntimeConfigKey]
            ?? configuration[LegacyAspireContainerRuntimeConfigKey])?.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(configuredRuntime))
        {
            if (configuredRuntime is not Docker and not Podman)
            {
                throw new InvalidOperationException(
                    $"Kind integration supports Aspire container runtimes '{Docker}' and '{Podman}', but '{configuredRuntime}' was configured. " +
                    $"Set {AspireContainerRuntimeConfigKey} to '{Docker}' or '{Podman}'.");
            }

            var configuredProbe = await ProbeRuntimeAsync(configuredRuntime, logger, cancellationToken).ConfigureAwait(false);
            if (!configuredProbe.IsAvailable)
            {
                throw new InvalidOperationException(
                    $"The configured Aspire container runtime '{configuredRuntime}' is not available or not running: {configuredProbe.Error}");
            }

            return new KindContainerRuntime(configuredRuntime);
        }

        var probes = await Task.WhenAll(
            ProbeRuntimeAsync(Docker, logger, cancellationToken),
            ProbeRuntimeAsync(Podman, logger, cancellationToken)).ConfigureAwait(false);

        var dockerProbe = probes[0];
        var podmanProbe = probes[1];

        if (dockerProbe.IsAvailable)
        {
            return new KindContainerRuntime(Docker);
        }

        if (podmanProbe.IsAvailable)
        {
            return new KindContainerRuntime(Podman);
        }

        throw new InvalidOperationException(
            $"Kind integration requires a running '{Docker}' or '{Podman}' container runtime. " +
            $"{Docker}: {dockerProbe.Error}; {Podman}: {podmanProbe.Error}");
    }

    private async Task<RuntimeProbeResult> ProbeRuntimeAsync(
        string executable,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await processRunner.RunAsync(
                logger,
                executable,
                ["container", "ls", "-n", "1"],
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result.ExitCode == 0
                ? RuntimeProbeResult.Available
                : new RuntimeProbeResult(false, string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error);
        }
        catch (Win32Exception ex)
        {
            return new RuntimeProbeResult(false, ex.Message);
        }
    }

    private readonly record struct RuntimeProbeResult(bool IsAvailable, string Error)
    {
        public static RuntimeProbeResult Available { get; } = new(true, "");
    }
}

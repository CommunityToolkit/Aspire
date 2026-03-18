// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Dapr;

[AspireDto]
internal sealed record DaprComponentExportOptions
{
    public string? LocalPath { get; init; }

    public DaprComponentOptions ToDaprComponentOptions()
    {
        return new DaprComponentOptions
        {
            LocalPath = LocalPath
        };
    }
}

[AspireDto]
internal sealed record DaprSidecarExportOptions
{
    public string? AppChannelAddress { get; init; }
    public string? AppHealthCheckPath { get; init; }
    public int? AppHealthProbeInterval { get; init; }
    public int? AppHealthProbeTimeout { get; init; }
    public int? AppHealthThreshold { get; init; }
    public string? AppId { get; init; }
    public int? AppMaxConcurrency { get; init; }
    public int? AppPort { get; init; }
    public string? AppProtocol { get; init; }
    public string? AppEndpoint { get; init; }
    public string[]? Command { get; init; }
    public string? Config { get; init; }
    public int? DaprGrpcPort { get; init; }
    public int? DaprHttpMaxRequestSize { get; init; }
    public string? DaprMaxBodySize { get; init; }
    public int? DaprHttpPort { get; init; }
    public int? DaprHttpReadBufferSize { get; init; }
    public string? DaprReadBufferSize { get; init; }
    public int? DaprInternalGrpcPort { get; init; }
    public string? DaprListenAddresses { get; init; }
    public bool? EnableApiLogging { get; init; }
    public bool? EnableAppHealthCheck { get; init; }
    public bool? EnableProfiling { get; init; }
    public string? LogLevel { get; init; }
    public int? MetricsPort { get; init; }
    public string? PlacementHostAddress { get; init; }
    public int? ProfilePort { get; init; }
    public string[]? ResourcesPaths { get; init; }
    public string? RunFile { get; init; }
    public string? RuntimePath { get; init; }
    public string? SchedulerHostAddress { get; init; }
    public string? UnixDomainSocket { get; init; }

    public DaprSidecarOptions ToDaprSidecarOptions()
    {
#pragma warning disable CS0618
        return new DaprSidecarOptions
        {
            AppChannelAddress = AppChannelAddress,
            AppHealthCheckPath = AppHealthCheckPath,
            AppHealthProbeInterval = AppHealthProbeInterval,
            AppHealthProbeTimeout = AppHealthProbeTimeout,
            AppHealthThreshold = AppHealthThreshold,
            AppId = AppId,
            AppMaxConcurrency = AppMaxConcurrency,
            AppPort = AppPort,
            AppProtocol = AppProtocol,
            AppEndpoint = AppEndpoint,
            Command = Command?.ToImmutableList() ?? ImmutableList<string>.Empty,
            Config = Config,
            DaprGrpcPort = DaprGrpcPort,
            DaprHttpMaxRequestSize = DaprHttpMaxRequestSize,
            DaprMaxBodySize = DaprMaxBodySize,
            DaprHttpPort = DaprHttpPort,
            DaprHttpReadBufferSize = DaprHttpReadBufferSize,
            DaprReadBufferSize = DaprReadBufferSize,
            DaprInternalGrpcPort = DaprInternalGrpcPort,
            DaprListenAddresses = DaprListenAddresses,
            EnableApiLogging = EnableApiLogging,
            EnableAppHealthCheck = EnableAppHealthCheck,
            EnableProfiling = EnableProfiling,
            LogLevel = LogLevel,
            MetricsPort = MetricsPort,
            PlacementHostAddress = PlacementHostAddress,
            ProfilePort = ProfilePort,
            ResourcesPaths = ResourcesPaths?.ToImmutableHashSet(StringComparer.Ordinal) ?? ImmutableHashSet<string>.Empty,
            RunFile = RunFile,
            RuntimePath = RuntimePath,
            SchedulerHostAddress = SchedulerHostAddress,
            UnixDomainSocket = UnixDomainSocket
        };
#pragma warning restore CS0618
    }
}

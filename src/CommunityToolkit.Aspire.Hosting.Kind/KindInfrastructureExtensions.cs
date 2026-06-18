// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Registers shared infrastructure services used by all Kind entry points.
/// </summary>
internal static class KindInfrastructureExtensions
{
    /// <summary>
    /// Registers core Kind services (process runner, etc.) into the DI container.
    /// Safe to call multiple times - uses TryAdd semantics.
    /// </summary>
    internal static IServiceCollection AddKindInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton<IProcessRunner, DefaultProcessRunner>();
        services.TryAddSingleton<IKindContainerRuntimeResolver, KindContainerRuntimeResolver>();
        return services;
    }
}

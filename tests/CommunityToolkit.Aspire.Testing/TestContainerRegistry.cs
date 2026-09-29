// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Helper for tests that create containers directly via the Testcontainers library (i.e. not through
/// Aspire's container resource model) so that they can also be routed through a custom container registry.
/// </summary>
public static class TestContainerRegistry
{
    private const string DockerHubRegistry = "docker.io";

    /// <summary>
    /// Resolves the registry to use for a Testcontainers image reference. If <paramref name="registry"/> is the
    /// default Docker Hub registry and the <c>CUSTOM_CONTAINER_REGISTRY</c> environment variable is set, the
    /// custom registry is returned instead to avoid Docker Hub rate limiting. Otherwise, <paramref name="registry"/>
    /// is returned unchanged.
    /// </summary>
    /// <param name="registry">The registry the image would otherwise be pulled from.</param>
    public static string Resolve(string registry)
    {
        string? customRegistry = Environment.GetEnvironmentVariable("CUSTOM_CONTAINER_REGISTRY");

        return registry == DockerHubRegistry && !string.IsNullOrEmpty(customRegistry)
            ? customRegistry
            : registry;
    }
}

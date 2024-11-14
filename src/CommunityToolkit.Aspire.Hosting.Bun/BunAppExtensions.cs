using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Bun;
using CommunityToolkit.Aspire.Utils;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding a Bun app to a <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class BunAppExtensions
{
    /// <summary>
    /// Adds a Bun app to the builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="entryPoint">The entry point, either a file or package.json script name.</param>
    /// <param name="watch">Whether to watch for changes.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<BunAppResource> AddBunApp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string? workingDirectory = null,
        string entryPoint = "index.ts",
        bool watch = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentException.ThrowIfNullOrEmpty(entryPoint, nameof(entryPoint));

        workingDirectory ??= Path.Combine("..", name);

        var resource = new BunAppResource(name, PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory)));

        string[] args = watch ? ["--watch", "run", entryPoint] : ["run", entryPoint];

        return builder.AddResource(resource)
            .WithBunDefaults()
            .WithArgs(args);
    }

    /// <summary>
    /// Ensures the Bun packages are installed before the application starts using Bun as the package manager.
    /// </summary>
    /// <param name="resource">The Bun app resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<BunAppResource> WithBunPackageInstallation(this IResourceBuilder<BunAppResource> resource)
    {
        resource.ApplicationBuilder.Services.TryAddLifecycleHook<BunPackageInstallerLifecycleHook>();
        return resource;
    }

    private static IResourceBuilder<BunAppResource> WithBunDefaults(
        this IResourceBuilder<BunAppResource> builder) => builder
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production")
            .WithOtlpExporter();
}

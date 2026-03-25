#pragma warning disable ASPIREATS001 // AspireExport is experimental.

using Aspire.Hosting.ApplicationModel;
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
    [AspireExport("addBunApp", Description = "Adds a Bun app")]
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
    /// <param name="configureInstaller">Configure the Bun installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithBunPackageInstallation(IResourceBuilder{BunAppResource})"/> instead.</remarks>
    [AspireExportIgnore(Reason = "Action<IResourceBuilder<BunInstallerResource>> is not ATS-compatible. Use the overload without configureInstaller instead.")]
    public static IResourceBuilder<BunAppResource> WithBunPackageInstallation(this IResourceBuilder<BunAppResource> resource, Action<IResourceBuilder<BunInstallerResource>>? configureInstaller = null)
        => WithBunPackageInstallationCore(resource, configureInstaller);

    /// <summary>
    /// Ensures the Bun packages are installed before the application starts using Bun as the package manager.
    /// </summary>
    [AspireExport("withBunPackageInstallation", Description = "Installs Bun packages before the app starts")]
    internal static IResourceBuilder<BunAppResource> WithBunPackageInstallation(this IResourceBuilder<BunAppResource> resource)
        => WithBunPackageInstallationCore(resource, configureInstaller: null);

    private static IResourceBuilder<BunAppResource> WithBunPackageInstallationCore(
        this IResourceBuilder<BunAppResource> resource,
        Action<IResourceBuilder<BunInstallerResource>>? configureInstaller)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-bun-install";

            if (resource.ApplicationBuilder.TryCreateResourceBuilder<BunInstallerResource>(installerName, out var existingInstaller))
            {
                configureInstaller?.Invoke(existingInstaller);
                return resource;
            }

            var installer = new BunInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs("install")
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            // Make the parent resource wait for the installer to complete
            resource.WaitForCompletion(installerBuilder);

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }

    private static IResourceBuilder<BunAppResource> WithBunDefaults(
        this IResourceBuilder<BunAppResource> builder) => builder
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production")
            .WithOtlpExporter();
}

#pragma warning restore ASPIREATS001

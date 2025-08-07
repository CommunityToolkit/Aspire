using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Node.js applications to the distributed application builder.
/// </summary>
public static partial class NodeJSHostingExtensions
{
    /// <summary>
    /// Adds a Node.js app to the distributed application builder using yarn as the package manager.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="scriptName">The npm script to execute. Defaults to "start".</param>
    /// <param name="args">The arguments to pass to the command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> AddYarnApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string scriptName = "start", string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(scriptName);
        string[] allArgs = args is { Length: > 0 }
            ? ["run", scriptName, "--", .. args]
            : ["run", scriptName];

        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));
        var resource = new NodeAppResource(name, "yarn", workingDirectory);

        return builder.AddResource(resource)
                      .WithNodeDefaults()
                      .WithArgs(allArgs);
    }

    /// <summary>
    /// Adds a Node.js app to the distributed application builder using pnpm as the package manager.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="scriptName">The npm script to execute. Defaults to "start".</param>
    /// <param name="args">The arguments to pass to the command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> AddPnpmApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string scriptName = "start", string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(scriptName);

        string[] allArgs = args is { Length: > 0 }
            ? ["run", scriptName, "--", .. args]
            : ["run", scriptName];

        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));
        var resource = new NodeAppResource(name, "pnpm", workingDirectory);
        return builder.AddResource(resource)
                      .WithNodeDefaults()
                      .WithArgs(allArgs);
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the application starts using npm as the package manager.
    /// </summary>
    /// <param name="resource">The Node.js app resource.</param>
    /// <param name="useCI">When true use <code>npm ci</code> otherwise use <code>npm install</code> when installing packages.</param>
    /// <param name="configureInstaller">Configure the npm installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> WithNpmPackageInstallation(this IResourceBuilder<NodeAppResource> resource, bool useCI = false, Action<IResourceBuilder<NpmInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-npm-install";
            var installer = new NpmInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs([useCI ? "ci" : "install"])
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            // Make the parent resource wait for the installer to complete
            resource.WaitForCompletion(installerBuilder);

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the application starts using yarn as the package manager.
    /// </summary>
    /// <param name="resource">The Node.js app resource.</param>
    /// <param name="configureInstaller">Configure the yarn installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> WithYarnPackageInstallation(this IResourceBuilder<NodeAppResource> resource, Action<IResourceBuilder<YarnInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-yarn-install";
            var installer = new YarnInstallerResource(installerName, resource.Resource.WorkingDirectory);

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

    /// <summary>
    /// Ensures the Node.js packages are installed before the application starts using pnpm as the package manager.
    /// </summary>
    /// <param name="resource">The Node.js app resource.</param>
    /// <param name="configureInstaller">Configure the pnpm installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> WithPnpmPackageInstallation(this IResourceBuilder<NodeAppResource> resource, Action<IResourceBuilder<PnpmInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-pnpm-install";
            var installer = new PnpmInstallerResource(installerName, resource.Resource.WorkingDirectory);

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

    /// <summary>
    /// Ensures the Node.js packages are installed before the Nx workspace applications start using npm as the package manager.
    /// </summary>
    /// <param name="resource">The Nx workspace resource.</param>
    /// <param name="useCI">When true use <code>npm ci</code> otherwise use <code>npm install</code> when installing packages.</param>
    /// <param name="configureInstaller">Configure the npm installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxResource> WithNpmPackageInstaller(this IResourceBuilder<NxResource> resource, bool useCI = false, Action<IResourceBuilder<NpmInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-npm-install";
            var installer = new NpmInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs([useCI ? "ci" : "install"])
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the Nx workspace applications start using yarn as the package manager.
    /// </summary>
    /// <param name="resource">The Nx workspace resource.</param>
    /// <param name="configureInstaller">Configure the yarn installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxResource> WithYarnPackageInstaller(this IResourceBuilder<NxResource> resource, Action<IResourceBuilder<YarnInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-yarn-install";
            var installer = new YarnInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs("install")
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the Nx workspace applications start using pnpm as the package manager.
    /// </summary>
    /// <param name="resource">The Nx workspace resource.</param>
    /// <param name="configureInstaller">Configure the pnpm installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxResource> WithPnpmPackageInstaller(this IResourceBuilder<NxResource> resource, Action<IResourceBuilder<PnpmInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-pnpm-install";
            var installer = new PnpmInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs("install")
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the Turborepo workspace applications start using npm as the package manager.
    /// </summary>
    /// <param name="resource">The Turborepo workspace resource.</param>
    /// <param name="useCI">When true use <code>npm ci</code> otherwise use <code>npm install</code> when installing packages.</param>
    /// <param name="configureInstaller">Configure the npm installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoResource> WithNpmPackageInstaller(this IResourceBuilder<TurborepoResource> resource, bool useCI = false, Action<IResourceBuilder<NpmInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-npm-install";
            var installer = new NpmInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs([useCI ? "ci" : "install"])
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the Turborepo workspace applications start using yarn as the package manager.
    /// </summary>
    /// <param name="resource">The Turborepo workspace resource.</param>
    /// <param name="configureInstaller">Configure the yarn installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoResource> WithYarnPackageInstaller(this IResourceBuilder<TurborepoResource> resource, Action<IResourceBuilder<YarnInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-yarn-install";
            var installer = new YarnInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs("install")
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the Turborepo workspace applications start using pnpm as the package manager.
    /// </summary>
    /// <param name="resource">The Turborepo workspace resource.</param>
    /// <param name="configureInstaller">Configure the pnpm installer resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoResource> WithPnpmPackageInstaller(this IResourceBuilder<TurborepoResource> resource, Action<IResourceBuilder<PnpmInstallerResource>>? configureInstaller = null)
    {
        // Only install packages during development, not in publish mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            var installerName = $"{resource.Resource.Name}-pnpm-install";
            var installer = new PnpmInstallerResource(installerName, resource.Resource.WorkingDirectory);

            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithArgs("install")
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            configureInstaller?.Invoke(installerBuilder);
        }

        return resource;
    }
}

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Node.js applications to the distributed application builder.
/// </summary>
public static partial class NodeJSHostingExtensions
{


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

            resource.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
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

            resource.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
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

            resource.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
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

            resource.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
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

            resource.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
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

            resource.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
        }

        return resource;
    }
}

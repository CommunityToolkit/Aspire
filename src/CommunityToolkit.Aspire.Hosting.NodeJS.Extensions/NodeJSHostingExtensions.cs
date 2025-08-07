using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Node.js applications to the distributed application builder.
/// </summary>
public static class NodeJSHostingExtensions
{
    /// <summary>
    /// Adds a Vite app to the distributed application builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the Vite app.</param>
    /// <param name="workingDirectory">The working directory of the Vite app. If not specified, it will be set to a path that is a sibling of the AppHost directory using the <paramref name="name"/> as the folder.</param>
    /// <param name="packageManager">The package manager to use. Default is npm.</param>
    /// <param name="useHttps">When true use HTTPS for the endpoints, otherwise use HTTP.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This uses the specified package manager (default npm) method internally but sets defaults that would be expected to run a Vite app, such as the command to run the dev server and exposing the HTTP endpoints.</remarks>
    public static IResourceBuilder<NodeAppResource> AddViteApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? workingDirectory = null, string packageManager = "npm", bool useHttps = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(packageManager);

        string wd = workingDirectory ?? Path.Combine("..", name);

        var resource = packageManager switch
        {
            "yarn" => builder.AddYarnApp(name, wd, "dev"),
            "pnpm" => builder.AddPnpmApp(name, wd, "dev"),
            _ => builder.AddNpmApp(name, wd, "dev")
        };

        _ = useHttps
            ? resource.WithHttpsEndpoint(env: "PORT")
            : resource.WithHttpEndpoint(env: "PORT");

        return resource.WithArgs(ctx =>
        {
            if (packageManager == "npm")
            {
                ctx.Args.Add("--");
            }

            var targetEndpoint = resource.Resource.GetEndpoint(useHttps ? "https" : "http");
            ctx.Args.Add("--port");
            ctx.Args.Add(targetEndpoint.Property(EndpointProperty.TargetPort));
        });
    }

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
    /// Adds an Nx monorepo workspace to the distributed application builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the Nx workspace resource.</param>
    /// <param name="workingDirectory">The working directory of the Nx workspace. If not specified, it will be set to a path that is a sibling of the AppHost directory using the <paramref name="name"/> as the folder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxResource> AddNxApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        string wd = workingDirectory ?? Path.Combine("..", name);
        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        var resource = new NxResource(name, workingDirectory);
        return builder.AddResource(resource);
    }

    /// <summary>
    /// Adds a Turborepo monorepo workspace to the distributed application builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the Turborepo workspace resource.</param>
    /// <param name="workingDirectory">The working directory of the Turborepo workspace. If not specified, it will be set to a path that is a sibling of the AppHost directory using the <paramref name="name"/> as the folder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoResource> AddTurborepoApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        string wd = workingDirectory ?? Path.Combine("..", name);
        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        var resource = new TurborepoResource(name, workingDirectory);
        return builder.AddResource(resource);
    }

    /// <summary>
    /// Adds an individual app to an Nx workspace.
    /// </summary>
    /// <param name="builder">The Nx workspace resource builder.</param>
    /// <param name="name">The name of the app resource.</param>
    /// <param name="appName">The Nx app name to run. If not specified, uses the <paramref name="name"/>.</param>
    /// <param name="configure">A function to configure the app resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxAppResource> AddApp(this IResourceBuilder<NxResource> builder, [ResourceName] string name, string? appName = null, Func<IResourceBuilder<NxAppResource>, IResourceBuilder<NxAppResource>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        appName ??= name;
        var resource = new NxAppResource(name, builder.Resource.WorkingDirectory, appName);

        var rb = builder.ApplicationBuilder.AddResource(resource)
            .WithNodeDefaults()
            .WithArgs("serve", appName)
            .WithParentRelationship(builder.Resource);

        configure?.Invoke(rb);

        return rb;
    }

    /// <summary>
    /// Adds an individual app to a Turborepo workspace.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="name">The name of the app resource.</param>
    /// <param name="filter">The Turborepo filter to use. If not specified, uses the <paramref name="name"/>.</param>
    /// <param name="configure">A function to configure the app resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoAppResource> AddApp(this IResourceBuilder<TurborepoResource> builder, [ResourceName] string name, string? filter = null, Func<IResourceBuilder<TurborepoAppResource>, IResourceBuilder<TurborepoAppResource>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        filter ??= name;
        var resource = new TurborepoAppResource(name, builder.Resource.WorkingDirectory, filter);

        var rb = builder.ApplicationBuilder.AddResource(resource)
            .WithNodeDefaults()
            .WithArgs("run", "dev", "--filter", filter)
            .WithParentRelationship(builder.Resource);

        configure?.Invoke(rb);

        return rb;
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

    // Copied from https://github.com/dotnet/aspire/blob/50ca9fa670af5c70782dc75d2961956b06f1a403/src/Aspire.Hosting.NodeJs/NodeExtensions.cs#L70-L72
    private static IResourceBuilder<NodeAppResource> WithNodeDefaults(this IResourceBuilder<NodeAppResource> builder) =>
        builder.WithOtlpExporter()
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production");

    // Apply node defaults to NxAppResource as well
    private static IResourceBuilder<NxAppResource> WithNodeDefaults(this IResourceBuilder<NxAppResource> builder) =>
        builder.WithOtlpExporter()
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production");

    // Apply node defaults to TurborepoAppResource as well
    private static IResourceBuilder<TurborepoAppResource> WithNodeDefaults(this IResourceBuilder<TurborepoAppResource> builder) =>
        builder.WithOtlpExporter()
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production");
}

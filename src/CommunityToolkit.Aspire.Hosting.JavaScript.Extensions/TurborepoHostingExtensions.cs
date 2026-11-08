using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Turborepo applications to the distributed application builder.
/// </summary>
public static class TurborepoHostingExtensions
{
    internal const string CliName = "turbo";
    
    /// <summary>
    /// Adds a Turborepo monorepo workspace to the distributed application builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the Turborepo workspace resource.</param>
    /// <param name="workingDirectory">The working directory of the Turborepo workspace. If not specified, it will be set to a path that is a sibling of the AppHost directory using the <paramref name="name"/> as the folder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<TurborepoResource> AddTurborepoApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        string wd = workingDirectory ?? Path.Combine("..", name);
        workingDirectory = Path.Combine(builder.AppHostDirectory, wd).NormalizePathForCurrentPlatform();

        var resource = new TurborepoResource(name, workingDirectory);
        return builder.AddResource(resource)
            .WithIconName("CodeJsRectangle")
            .WithInitialState(new CustomResourceSnapshot { Properties = [], ResourceType = "TurborepoWorkspace", State = KnownResourceStates.Running });
    }

    /// <summary>
    /// Adds an individual app to a Turborepo workspace.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="name">The name of the app resource.</param>
    /// <param name="filter">The Turborepo filter to use. If not specified, uses the <paramref name="name"/>.</param>
    /// <param name="configure">A function to configure the app resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="AddTurborepoWorkspaceApp(IResourceBuilder{TurborepoResource}, string, string?)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The configure callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<TurborepoAppResource> AddApp(this IResourceBuilder<TurborepoResource> builder, [ResourceName] string name, string? filter = null, Func<IResourceBuilder<TurborepoAppResource>, IResourceBuilder<TurborepoAppResource>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        filter ??= name;

        var resource = new TurborepoAppResource(name, builder.Resource.WorkingDirectory, filter);

        var rb = builder.ApplicationBuilder.AddResource(resource)
            .WithNodeDefaults()
            .WithIconName("CodeJsRectangle")
            .WithArgs("run", "dev", "--filter", filter)
            .WithParentRelationship(builder.Resource);

        // If the workspace is configured to use a package manager, bring that annotation forward
        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var executionAnnotation))
        {
            rb.WithCommand(executionAnnotation.ExecutableName switch
            {
                "npm" => "npx",
                "yarn" => "yarn",
                "pnpm" => "pnpx",
                "bun" => "bunx",
                _ => executionAnnotation.ExecutableName
            })
              .WithArgs(context =>
              {
                  context.Args.Insert(0, executionAnnotation.ScriptCommand ?? CliName);
              });
        }

        // If the workspace has an installer annotation, wait for the installer to complete
        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation))
        {
            rb.WaitForCompletion(builder.ApplicationBuilder.CreateResourceBuilder(installerAnnotation.Resource));
        }

        configure?.Invoke(rb);

        return rb;
    }

    [AspireExport(MethodName = "addApp")]
    internal static IResourceBuilder<TurborepoAppResource> AddTurborepoWorkspaceApp(this IResourceBuilder<TurborepoResource> builder, [ResourceName] string name, string? filter = null) =>
        builder.AddApp(name, filter, configure: null);
    
    /// <summary>
    /// Configures the Turborepo workspace to use the specified JavaScript package manager when starting apps.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="packageManager">The package manager to use. If none is provided it will attempt to use the installer annotation's resource command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Turborepo workspace is already configured to use a different package manager.</exception>
    [AspireExport("withTurborepoPackageManagerLaunch", MethodName = "withPackageManagerLaunch")]
    public static IResourceBuilder<TurborepoResource> WithPackageManagerLaunch(this IResourceBuilder<TurborepoResource> builder, string? packageManager = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var existingExecution))
        {
            if (packageManager is null || existingExecution.ExecutableName == packageManager)
            {
                // already configured with execution annotation
                return builder;
            }
            throw new InvalidOperationException($"The Turborepo workspace '{builder.Resource.Name}' is already configured to run with the '{existingExecution.ExecutableName}' package manager.");
        }

        // If no explicit package manager provided, try to infer from configured annotation
        if (packageManager is null)
        {
            if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageManagerConfiguredAnnotation>(out var configured))
            {
                packageManager = configured.PackageManager;
            }
            else
            {
                throw new InvalidOperationException($"The Turborepo workspace '{builder.Resource.Name}' is not configured with a package manager. Call WithNpm/WithYarn/WithPnpm/WithBun first, or provide an explicit package manager.");
            }
        }

        return builder.WithAnnotation(new JavaScriptPackageManagerAnnotation(packageManager, runScriptCommand: CliName));
    }

    /// <summary>
    /// Configures the Turborepo workspace to use npm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithTurborepoNpm(IResourceBuilder{TurborepoResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<TurborepoResource> WithNpm(this IResourceBuilder<TurborepoResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("npm", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withNpm")]
    internal static IResourceBuilder<TurborepoResource> WithTurborepoNpm(this IResourceBuilder<TurborepoResource> builder, bool install = false) =>
        builder.WithNpm(install, configureInstaller: null);

    /// <summary>
    /// Configures the Turborepo workspace to use yarn as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithTurborepoYarn(IResourceBuilder{TurborepoResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<TurborepoResource> WithYarn(this IResourceBuilder<TurborepoResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("yarn", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withYarn")]
    internal static IResourceBuilder<TurborepoResource> WithTurborepoYarn(this IResourceBuilder<TurborepoResource> builder, bool install = false) =>
        builder.WithYarn(install, configureInstaller: null);

    /// <summary>
    /// Configures the Turborepo workspace to use pnpm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithTurborepoPnpm(IResourceBuilder{TurborepoResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<TurborepoResource> WithPnpm(this IResourceBuilder<TurborepoResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("pnpm", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withPnpm")]
    internal static IResourceBuilder<TurborepoResource> WithTurborepoPnpm(this IResourceBuilder<TurborepoResource> builder, bool install = false) =>
        builder.WithPnpm(install, configureInstaller: null);

    /// <summary>
    /// Configures the Turborepo workspace to use bun as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithTurborepoBun(IResourceBuilder{TurborepoResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<TurborepoResource> WithBun(this IResourceBuilder<TurborepoResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("bun", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withBun")]
    internal static IResourceBuilder<TurborepoResource> WithTurborepoBun(this IResourceBuilder<TurborepoResource> builder, bool install = false) =>
        builder.WithBun(install, configureInstaller: null);
}
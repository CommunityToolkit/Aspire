using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Vite+ applications to the distributed application builder.
/// </summary>
public static class VitePlusHostingExtensions
{
    internal const string CliName = "vp";
    
    /// <summary>
    /// Adds a Vite+ monorepo workspace to the distributed application builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the Vite+ workspace resource.</param>
    /// <param name="workingDirectory">The working directory of the Vite+ workspace. If not specified, it will be set to a path that is a sibling of the AppHost directory using the <paramref name="name"/> as the folder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<VitePlusResource> AddVitePlusApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        string wd = workingDirectory ?? Path.Combine("..", name);
        workingDirectory = Path.Combine(builder.AppHostDirectory, wd).NormalizePathForCurrentPlatform();

        var resource = new VitePlusResource(name, workingDirectory);
        return builder.AddResource(resource)
            .WithIconName("CodeJsRectangle")
            .WithInitialState(new CustomResourceSnapshot { Properties = [], ResourceType = "VitePlusWorkspace", State = KnownResourceStates.Running });
    }

    /// <summary>
    /// Adds an individual app to a Vite+ workspace.
    /// </summary>
    /// <param name="builder">The Vite+ workspace resource builder.</param>
    /// <param name="name">The name of the app resource.</param>
    /// <param name="directory">The directory of the app within the Vite+ workspace, relative to the workspace root. It is passed to <c>vp -C</c> so the app behaves as if you had cd'd into it. If not specified, defaults to <c>apps/{name}</c>.</param>
    /// <param name="configure">A function to configure the app resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="AddVitePlusWorkspaceApp(IResourceBuilder{VitePlusResource}, string, string?)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The configure callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<VitePlusAppResource> AddApp(this IResourceBuilder<VitePlusResource> builder, [ResourceName] string name, string? directory = null, Func<IResourceBuilder<VitePlusAppResource>, IResourceBuilder<VitePlusAppResource>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        directory ??= Path.Combine("apps", name);

        var resource = new VitePlusAppResource(name, builder.Resource.WorkingDirectory, directory);

        var rb = builder.ApplicationBuilder.AddResource(resource)
            .WithNodeDefaults()
            .WithIconName("CodeJsRectangle")
            .WithArgs("-C", directory, "dev")
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
    internal static IResourceBuilder<VitePlusAppResource> AddVitePlusWorkspaceApp(this IResourceBuilder<VitePlusResource> builder, [ResourceName] string name, string? directory = null) =>
        builder.AddApp(name, directory, configure: null);
    
    /// <summary>
    /// Configures the Vite+ workspace to use the specified JavaScript package manager when starting apps.
    /// </summary>
    /// <param name="builder">The Vite+ workspace resource builder.</param>
    /// <param name="packageManager">The package manager to use. If none is provided it will attempt to use the installer annotation's resource command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Vite+ workspace is already configured to use a different package manager.</exception>
    [AspireExport("withVitePlusPackageManagerLaunch", MethodName = "withPackageManagerLaunch")]
    public static IResourceBuilder<VitePlusResource> WithPackageManagerLaunch(this IResourceBuilder<VitePlusResource> builder, string? packageManager = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var existingExecution))
        {
            if (packageManager is null || existingExecution.ExecutableName == packageManager)
            {
                // already configured with execution annotation
                return builder;
            }
            throw new InvalidOperationException($"The Vite+ workspace '{builder.Resource.Name}' is already configured to run with the '{existingExecution.ExecutableName}' package manager.");
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
                throw new InvalidOperationException($"The Vite+ workspace '{builder.Resource.Name}' is not configured with a package manager. Call WithNpm/WithYarn/WithPnpm/WithBun first, or provide an explicit package manager.");
            }
        }

        return builder.WithAnnotation(new JavaScriptPackageManagerAnnotation(packageManager, runScriptCommand: CliName));
    }

    /// <summary>
    /// Configures the Vite+ workspace to use npm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Vite+ workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithVitePlusNpm(IResourceBuilder{VitePlusResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<VitePlusResource> WithNpm(this IResourceBuilder<VitePlusResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("npm", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withNpm")]
    internal static IResourceBuilder<VitePlusResource> WithVitePlusNpm(this IResourceBuilder<VitePlusResource> builder, bool install = false) =>
        builder.WithNpm(install, configureInstaller: null);

    /// <summary>
    /// Configures the Vite+ workspace to use yarn as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Vite+ workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithVitePlusYarn(IResourceBuilder{VitePlusResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<VitePlusResource> WithYarn(this IResourceBuilder<VitePlusResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("yarn", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withYarn")]
    internal static IResourceBuilder<VitePlusResource> WithVitePlusYarn(this IResourceBuilder<VitePlusResource> builder, bool install = false) =>
        builder.WithYarn(install, configureInstaller: null);

    /// <summary>
    /// Configures the Vite+ workspace to use pnpm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Vite+ workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithVitePlusPnpm(IResourceBuilder{VitePlusResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<VitePlusResource> WithPnpm(this IResourceBuilder<VitePlusResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("pnpm", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withPnpm")]
    internal static IResourceBuilder<VitePlusResource> WithVitePlusPnpm(this IResourceBuilder<VitePlusResource> builder, bool install = false) =>
        builder.WithPnpm(install, configureInstaller: null);

    /// <summary>
    /// Configures the Vite+ workspace to use bun as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Vite+ workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>This overload is not available in polyglot app hosts. Use <see cref="WithVitePlusBun(IResourceBuilder{VitePlusResource}, bool)"/> instead.</remarks>
    [AspireExportIgnore(Reason = "The installer configuration callback is not ATS-compatible. Use the overload without the configure callback instead.")]
    public static IResourceBuilder<VitePlusResource> WithBun(this IResourceBuilder<VitePlusResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddMonorepoInstaller("bun", install, configureInstaller);
        return builder;
    }

    [AspireExport(MethodName = "withBun")]
    internal static IResourceBuilder<VitePlusResource> WithVitePlusBun(this IResourceBuilder<VitePlusResource> builder, bool install = false) =>
        builder.WithBun(install, configureInstaller: null);
}
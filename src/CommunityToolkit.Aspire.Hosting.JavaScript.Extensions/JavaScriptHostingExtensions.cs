using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;
using CommunityToolkit.Aspire.Utils;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding JavaScript applications to the distributed application builder.
/// </summary>
public static partial class JavaScriptHostingExtensions
{
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
        return builder.AddResource(resource)
            .WithIconName("CodeJsRectangle")
            .WithInitialState(new CustomResourceSnapshot { Properties = [], ResourceType = "NxWorkspace", State = KnownResourceStates.Running });
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
        return builder.AddResource(resource)
            .WithIconName("CodeJsRectangle")
            .WithInitialState(new CustomResourceSnapshot { Properties = [], ResourceType = "TurborepoWorkspace", State = KnownResourceStates.Running });
    }

    /// <summary>
    /// Adds a yarn workspace to the distributed application builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the yarn workspace resource.</param>
    /// <param name="workingDirectory">The working directory of the workspace. If not specified, it will be set to a path that is a sibling of the AppHost directory using the <paramref name="name"/> as the folder.</param>
    /// <param name="install">When true (default), automatically installs packages before apps start. When false, only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<YarnWorkspaceResource> AddYarnWorkspaceApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? workingDirectory = null, bool install = true, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        string wd = workingDirectory ?? Path.Combine("..", name);
        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        var resource = new YarnWorkspaceResource(name, workingDirectory);

        var rb = builder.AddResource(resource)
            .WithIconName("CodeJsRectangle")
            .WithInitialState(new CustomResourceSnapshot { Properties = [], ResourceType = "YarnWorkspace", State = KnownResourceStates.Running })
            .WithAnnotation(new JavaScriptPackageManagerAnnotation("yarn", "run"));

        AddMonorepoInstaller(rb, "yarn", install, configureInstaller);

        return rb;
    }

    /// <summary>
    /// Adds a pnpm workspace to the distributed application builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the pnpm workspace resource.</param>
    /// <param name="workingDirectory">The working directory of the workspace. If not specified, it will be set to a path that is a sibling of the AppHost directory using the <paramref name="name"/> as the folder.</param>
    /// <param name="install">When true (default), automatically installs packages before apps start. When false, only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<PnpmWorkspaceResource> AddPnpmWorkspaceApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string? workingDirectory = null, bool install = true, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        string wd = workingDirectory ?? Path.Combine("..", name);
        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        var resource = new PnpmWorkspaceResource(name, workingDirectory);

        var rb = builder.AddResource(resource)
            .WithIconName("CodeJsRectangle")
            .WithInitialState(new CustomResourceSnapshot { Properties = [], ResourceType = "PnpmWorkspace", State = KnownResourceStates.Running })
            .WithAnnotation(new JavaScriptPackageManagerAnnotation("pnpm", "run"));

        AddMonorepoInstaller(rb, "pnpm", install, configureInstaller);

        return rb;
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
            .WithIconName("CodeJsRectangle")
            .WithArgs("serve", appName)
            .WithParentRelationship(builder.Resource);

        // If the workspace is configured to use a package manager, bring that annotation forward
        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var executionAnnotation))
        {
            rb.WithCommand(executionAnnotation.ExecutableName switch
            {
                "npm" => "npx",
                "yarn" => "yarn",
                "pnpm" => "pnpx",
                _ => executionAnnotation.ExecutableName
            })
              .WithArgs(context =>
              {
                  context.Args.Insert(0, executionAnnotation.ScriptCommand ?? "nx");
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
                _ => executionAnnotation.ExecutableName
            })
              .WithArgs(context =>
              {
                  context.Args.Insert(0, executionAnnotation.ScriptCommand ?? "nx");
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

    /// <summary>
    /// Adds an individual app from a yarn workspace.
    /// </summary>
    /// <param name="builder">The yarn workspace resource builder.</param>
    /// <param name="name">The name of the app resource.</param>
    /// <param name="workspaceName">The yarn workspace package name to run. Defaults to <paramref name="name"/>.</param>
    /// <param name="script">The script to run from the workspace package. Defaults to <c>dev</c>.</param>
    /// <param name="configure">A function to configure the app resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<YarnWorkspaceAppResource> AddApp(this IResourceBuilder<YarnWorkspaceResource> builder, [ResourceName] string name, string? workspaceName = null, string? script = null, Func<IResourceBuilder<YarnWorkspaceAppResource>, IResourceBuilder<YarnWorkspaceAppResource>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        workspaceName ??= name;
        script ??= "dev";

        var resource = new YarnWorkspaceAppResource(name, builder.Resource.WorkingDirectory, workspaceName, script);

        var rb = builder.ApplicationBuilder.AddResource(resource)
            .WithNodeDefaults()
            .WithIconName("CodeJsRectangle")
            .WithArgs("workspace", workspaceName, "run", script)
            .WithParentRelationship(builder.Resource);

        // If the workspace has an installer annotation, wait for the installer to complete
        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation))
        {
            rb.WaitForCompletion(builder.ApplicationBuilder.CreateResourceBuilder(installerAnnotation.Resource));
        }

        configure?.Invoke(rb);

        return rb;
    }

    /// <summary>
    /// Adds an individual app from a pnpm workspace.
    /// </summary>
    /// <param name="builder">The pnpm workspace resource builder.</param>
    /// <param name="name">The name of the app resource.</param>
    /// <param name="filter">The pnpm filter to use. Defaults to <paramref name="name"/>.</param>
    /// <param name="script">The script to run from the workspace package. Defaults to <c>dev</c>.</param>
    /// <param name="configure">A function to configure the app resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<PnpmWorkspaceAppResource> AddApp(this IResourceBuilder<PnpmWorkspaceResource> builder, [ResourceName] string name, string? filter = null, string? script = null, Func<IResourceBuilder<PnpmWorkspaceAppResource>, IResourceBuilder<PnpmWorkspaceAppResource>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        filter ??= name;
        script ??= "dev";

        var resource = new PnpmWorkspaceAppResource(name, builder.Resource.WorkingDirectory, filter, script);

        var rb = builder.ApplicationBuilder.AddResource(resource)
            .WithNodeDefaults()
            .WithIconName("CodeJsRectangle")
            .WithArgs("--filter", filter, "run", script)
            .WithParentRelationship(builder.Resource);

        // If the workspace has an installer annotation, wait for the installer to complete
        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation))
        {
            rb.WaitForCompletion(builder.ApplicationBuilder.CreateResourceBuilder(installerAnnotation.Resource));
        }

        configure?.Invoke(rb);

        return rb;
    }

    /// <summary>
    /// Configures the Nx workspace to use the specified JavaScript package manager when starting apps.
    /// </summary>
    /// <param name="builder">The Nx workspace resource builder.</param>
    /// <param name="packageManager">The package manager to use. If none is provided it will attempt to use the installer annotation's resource command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Nx workspace is already configured to use a different package manager.</exception>
    public static IResourceBuilder<NxResource> WithPackageManagerLaunch(this IResourceBuilder<NxResource> builder, string? packageManager = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var existingExecution))
        {
            if (packageManager is null || existingExecution.ExecutableName == packageManager)
            {
                // already configured with execution annotation
                return builder;
            }
            throw new InvalidOperationException($"The Nx workspace '{builder.Resource.Name}' is already configured to run with the '{existingExecution.ExecutableName}' package manager.");
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
                throw new InvalidOperationException($"The Nx workspace '{builder.Resource.Name}' is not configured with a package manager. Call WithNpm/WithYarn/WithPnpm first, or provide an explicit package manager.");
            }
        }

        return builder.WithAnnotation(new JavaScriptPackageManagerAnnotation(packageManager, runScriptCommand: "nx"));
    }

    /// <summary>
    /// Configures the Turborepo workspace to use the specified JavaScript package manager when starting apps.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="packageManager">The package manager to use. If none is provided it will attempt to use the installer annotation's resource command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Turborepo workspace is already configured to use a different package manager.</exception>
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
                throw new InvalidOperationException($"The Turborepo workspace '{builder.Resource.Name}' is not configured with a package manager. Call WithNpm/WithYarn/WithPnpm first, or provide an explicit package manager.");
            }
        }

        return builder.WithAnnotation(new JavaScriptPackageManagerAnnotation(packageManager, runScriptCommand: "turbo"));
    }

    /// <summary>
    /// Maps the endpoint port for the <see cref="JavaScriptAppResource"/> to the appropriate command line argument.
    /// </summary>
    /// <param name="builder">The Node.js app resource.</param>
    /// <param name="endpointName">The name of the endpoint to map. If not specified, it will use the first HTTP or HTTPS endpoint found.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TResource> WithMappedEndpointPort<TResource>(this IResourceBuilder<TResource> builder, string? endpointName = null) where TResource : JavaScriptAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithArgs(ctx =>
        {
            var resource = builder.Resource;

            // monorepo tools and npm (from Aspire.Hosting.JavaScript) need `--`, but yarn and pnpm don't
            if (!resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var executionAnnotation) || executionAnnotation.ExecutableName == "npm")
            {
                ctx.Args.Add("--");
            }

            // Find the target endpoint by name, or default to http/https if no name specified
            var targetEndpoint = endpointName is not null
                ? resource.GetEndpoint(endpointName)
                : resource.GetEndpoints().FirstOrDefault(e => e.EndpointName == "https") ?? resource.GetEndpoint("http");

            ctx.Args.Add("--port");
            ctx.Args.Add(targetEndpoint.Property(EndpointProperty.TargetPort));
        });
    }

    /// <summary>
    /// Configures the Nx workspace to use npm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Nx workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxResource> WithNpm(this IResourceBuilder<NxResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddMonorepoInstaller(builder, "npm", install, configureInstaller);
        return builder;
    }

    /// <summary>
    /// Configures the Nx workspace to use yarn as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Nx workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxResource> WithYarn(this IResourceBuilder<NxResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddMonorepoInstaller(builder, "yarn", install, configureInstaller);
        return builder;
    }

    /// <summary>
    /// Configures the Nx workspace to use pnpm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Nx workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NxResource> WithPnpm(this IResourceBuilder<NxResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddMonorepoInstaller(builder, "pnpm", install, configureInstaller);
        return builder;
    }

    /// <summary>
    /// Configures the Turborepo workspace to use npm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoResource> WithNpm(this IResourceBuilder<TurborepoResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddMonorepoInstaller(builder, "npm", install, configureInstaller);
        return builder;
    }

    /// <summary>
    /// Configures the Turborepo workspace to use yarn as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoResource> WithYarn(this IResourceBuilder<TurborepoResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddMonorepoInstaller(builder, "yarn", install, configureInstaller);
        return builder;
    }

    /// <summary>
    /// Configures the Turborepo workspace to use pnpm as the package manager and optionally installs packages before apps start.
    /// </summary>
    /// <param name="builder">The Turborepo workspace resource builder.</param>
    /// <param name="install">When true, automatically installs packages before apps start. When false (default), only sets the package manager annotation without creating an installer resource.</param>
    /// <param name="configureInstaller">A function to configure the installer resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TurborepoResource> WithPnpm(this IResourceBuilder<TurborepoResource> builder, bool install = false, Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddMonorepoInstaller(builder, "pnpm", install, configureInstaller);
        return builder;
    }

    private static void AddMonorepoInstaller<TResource>(
        IResourceBuilder<TResource> builder,
        string packageManager,
        bool install,
        Action<IResourceBuilder<JavaScriptInstallerResource>>? configureInstaller)
        where TResource : Resource
    {
        // Always add the configured annotation to track which package manager was set up
        builder.WithAnnotation(new JavaScriptPackageManagerConfiguredAnnotation(packageManager));

        // Only install packages if not in publish mode and install is true
        if (!builder.ApplicationBuilder.ExecutionContext.IsPublishMode && install)
        {
            var installerName = $"{builder.Resource.Name}-installer";
            var workingDirectory = builder.Resource switch
            {
                NxResource nx => nx.WorkingDirectory,
                TurborepoResource turbo => turbo.WorkingDirectory,
                YarnWorkspaceResource yarn => yarn.WorkingDirectory,
                PnpmWorkspaceResource pnpm => pnpm.WorkingDirectory,
                _ => throw new InvalidOperationException($"Unsupported resource type: {builder.Resource.GetType().Name}")
            };

            // Check if installer already exists
            if (builder.ApplicationBuilder.TryCreateResourceBuilder<JavaScriptInstallerResource>(installerName, out var existingResource))
            {
                // Installer already exists, don't create a new one
                return;
            }

            var installer = new JavaScriptInstallerResource(installerName, workingDirectory);

            var installerBuilder = builder.ApplicationBuilder
                .AddResource(installer)
                .WithCommand(packageManager)
                .WithParentRelationship(builder.Resource)
                .WithIconName("CodeJsRectangle")
                .ExcludeFromManifest();

            // Set up the installer command based on package manager
            installerBuilder.WithArgs("install");

            configureInstaller?.Invoke(installerBuilder);

            // Add annotation to track the installer
            builder.WithAnnotation(new JavaScriptPackageInstallerAnnotation(installer));
        }
    }

    private static IResourceBuilder<TResource> WithNodeDefaults<TResource>(this IResourceBuilder<TResource> builder) where TResource : JavaScriptAppResource =>
        builder.WithOtlpExporter()
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production")
            .WithCertificateTrustConfiguration((ctx) =>
            {
                if (ctx.Scope == CertificateTrustScope.Append)
                {
                    ctx.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = ctx.CertificateBundlePath;
                }
                else
                {
                    ctx.Arguments.Add("--use-openssl-ca");
                }

                return Task.CompletedTask;
            });
}

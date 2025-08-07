using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Node.js applications to the distributed application builder.
/// </summary>
public static partial class NodeJSHostingExtensions
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

        return resource
            .WithAnnotation(new JavaScriptPackageManagerAnnotation(packageManager))
            .WithMappedEndpointPort();
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

        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation))
        {
            rb.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(installerAnnotation.Resource));
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
            .WithArgs("run", "dev", "--filter", filter)
            .WithParentRelationship(builder.Resource);

        if (builder.Resource.TryGetLastAnnotation<JavaScriptPackageInstallerAnnotation>(out var installerAnnotation))
        {
            rb.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(installerAnnotation.Resource));
        }

        configure?.Invoke(rb);

        return rb;
    }

    /// <summary>
    /// Maps the endpoint port for the <see cref="NodeAppResource"/> to the appropriate command line argument.
    /// </summary>
    /// <param name="builder">The Node.js app resource.</param>
    /// <param name="endpointName">The name of the endpoint to map. If not specified, it will use the first HTTP or HTTPS endpoint found.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<TResource> WithMappedEndpointPort<TResource>(this IResourceBuilder<TResource> builder, string? endpointName = null) where TResource : NodeAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithArgs(ctx =>
        {
            var resource = builder.Resource;

            // monorepo tools will need `--`, as does npm
            if (!resource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var packageManagerAnnotation) || packageManagerAnnotation.PackageManager == "npm")
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

    // Copied from https://github.com/dotnet/aspire/blob/50ca9fa670af5c70782dc75d2961956b06f1a403/src/Aspire.Hosting.NodeJs/NodeExtensions.cs#L70-L72
    private static IResourceBuilder<TResource> WithNodeDefaults<TResource>(this IResourceBuilder<TResource> builder) where TResource : NodeAppResource =>
        builder.WithOtlpExporter()
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production");
}

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.NodeJS.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Aspire.Utils;

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

        return useHttps
            ? resource.WithHttpsEndpoint(env: "PORT").WithExternalHttpEndpoints()
            : resource.WithHttpEndpoint(env: "PORT").WithExternalHttpEndpoints();
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
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> WithNpmPackageInstallation(this IResourceBuilder<NodeAppResource> resource, bool useCI = false)
    {
        resource.ApplicationBuilder.Services.TryAddLifecycleHook<NpmPackageInstallerLifecycleHook>(sp =>
            new(useCI, sp.GetRequiredService<ResourceLoggerService>(), sp.GetRequiredService<ResourceNotificationService>(), sp.GetRequiredService<DistributedApplicationExecutionContext>()));
        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the application starts using yarn as the package manager.
    /// </summary>
    /// <param name="resource">The Node.js app resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> WithYarnPackageInstallation(this IResourceBuilder<NodeAppResource> resource)
    {
        resource.ApplicationBuilder.Services.TryAddLifecycleHook<YarnPackageInstallerLifecycleHook>();
        return resource;
    }

    /// <summary>
    /// Ensures the Node.js packages are installed before the application starts using pnpm as the package manager.
    /// </summary>
    /// <param name="resource">The Node.js app resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<NodeAppResource> WithPnpmPackageInstallation(this IResourceBuilder<NodeAppResource> resource)
    {
        resource.ApplicationBuilder.Services.TryAddLifecycleHook<PnpmPackageInstallerLifecycleHook>();
        return resource;
    }

    // Copied from https://github.com/dotnet/aspire/blob/50ca9fa670af5c70782dc75d2961956b06f1a403/src/Aspire.Hosting.NodeJs/NodeExtensions.cs#L70-L72
    private static IResourceBuilder<NodeAppResource> WithNodeDefaults(this IResourceBuilder<NodeAppResource> builder) =>
        builder.WithOtlpExporter()
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production");
}

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Hosting;
using CommunityToolkit.Aspire.Utils;

namespace CommunityToolkit.Aspire.Hosting.Deno;
/// <summary>
/// Extension methods to support adding Deno to the <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DenoAppHostingExtensions
{
    /// <summary>
    /// Adds a Deno application to the application model. Deno should available on the PATH.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="scriptPath">The path to the script that Deno will execute.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="permissionFlags">The permissions to grant to the program run.</param>
    /// <param name="args">The arguments to pass to the command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DenoAppResource> AddDenoApp(this IDistributedApplicationBuilder builder, string name, string scriptPath, string? workingDirectory = null, string[]? permissionFlags = null, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(scriptPath);

        string wd = workingDirectory ?? Path.Combine("..", name);

        args ??= [];
        permissionFlags ??= [];
        string[] effectiveArgs = ["run", .. permissionFlags, scriptPath, .. args];
        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        var resource = new DenoAppResource(name, "deno", workingDirectory);

        return builder.AddResource(resource)
                      .WithDenoDefaults()
                      .WithArgs(effectiveArgs);
    }

    /// <summary>
    /// Adds a Deno task to the distributed application builder 
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="taskName">The deno task to execute. Defaults to "start".</param>
    /// <param name="args">The arguments to pass to the command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DenoAppResource> AddDenoTask(this IDistributedApplicationBuilder builder, string name, string? workingDirectory = null, string taskName = "start", string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(taskName);

        string wd = workingDirectory ?? Path.Combine("..", name);

        args ??= [];
        string[] allArgs = ["task", taskName, .. args];


        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));
        var resource = new DenoAppResource(name, "deno", workingDirectory);

        return builder.AddResource(resource)
              .WithArgs(allArgs);
    }


    /// <summary>
    /// Ensures the Deno packages are installed before the application starts using Deno as the package manager.
    /// </summary>
    /// <param name="resource">The Deno app resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DenoAppResource> WithDenoPackageInstallation(this IResourceBuilder<DenoAppResource> resource)
    {
        resource.ApplicationBuilder.Services.TryAddLifecycleHook<DenoPackageInstallerLifecycleHook>();
        return resource;
    }

    private static IResourceBuilder<DenoAppResource> WithDenoDefaults(this IResourceBuilder<DenoAppResource> builder) =>
    builder.WithOtlpExporter()
        .WithEnvironment("DENO_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production");
}

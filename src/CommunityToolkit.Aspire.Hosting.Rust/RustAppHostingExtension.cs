using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Rust applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RustAppHostingExtension
{
    /// <summary>
    /// Adds a Rust application to the application model, using the cargo cli.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command.</param>
    /// <param name="args">The optional arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport("addRustApp", Description = "Adds a Rust application to the application model")]
    public static IResourceBuilder<RustAppExecutableResource> AddRustApp(
        this IDistributedApplicationBuilder builder, 
        [ResourceName] string name, 
        string workingDirectory, 
        string[]? args = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        string[] allArgs = args is { Length: > 0 }
            ? ["run", .. args]
            : ["run"];

        return builder.AddRustApp(name, workingDirectory, command: "cargo", allArgs);
    }
    
    /// <summary>
    /// Adds a Rust application to the application model, using the bacon cli.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command.</param>
    /// <param name="args">The optional arguments to be passed to the bacon command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport("addBaconApp", Description = "Adds a Rust application to the application model")]
    public static IResourceBuilder<RustAppExecutableResource> AddBaconApp(
        this IDistributedApplicationBuilder builder, 
        [ResourceName] string name, 
        string workingDirectory, 
        string[]? args = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        string[] allArgs = args is { Length: > 0 }
            ? args
            : ["run"];

        return builder.AddRustApp(name, workingDirectory, command: "bacon", allArgs);
    }

    private static IResourceBuilder<RustAppExecutableResource> AddRustApp(
        this IDistributedApplicationBuilder builder,
        string name,
        string workingDirectory,
        string command,
        string[] args)
    {
        workingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        var resource = new RustAppExecutableResource(name, workingDirectory, command);

        return builder.AddResource(resource)
                      .WithRustDefaults()
                      .WithArgs(args)
                      .PublishAsDockerFile();
    }

    private static IResourceBuilder<RustAppExecutableResource> WithRustDefaults(
        this IResourceBuilder<RustAppExecutableResource> builder) =>
        builder.WithOtlpExporter();
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental

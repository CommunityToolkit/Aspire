using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Rust;
using CommunityToolkit.Aspire.Utils;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Bacon applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RustAppHostingExtension
{
    /// <summary>
    /// Adds a Rust application to the application model, using the bacon cli.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command.</param>
    /// <param name="args">The optional arguments to be passed to the bacon command.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<RustAppResource> AddBaconApp(
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

        workingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        var resource = new RustAppResource(name, workingDirectory);

        return builder.AddResource(resource)
                      .WithCommand("bacon")
                      .WithOtlpExporter()
                      .WithArgs(allArgs)
                      .WithIconName("Code")
                      .PublishAsDockerFile();
    }
}

#pragma warning restore ASPIREATS001 // AspireExport is experimental

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Rust.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Rust applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RustAppHostingExtension
{
    /// <summary>
    /// Adds a Rust application to the application model. Executes the executable Rust app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="port">This is the port that will be given to other resource to communicate with this resource.</param>"
    /// <param name="args">The optinal arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<RustAppExecutableResource> AddRustApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, int port = 8080, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        string[] allArgs = args is { Length: > 0 }
            ? ["run", ".", .. args]
            : ["run", ".",];

        workingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        var resource = new RustAppExecutableResource(name, workingDirectory);

        return builder.AddResource(resource)
                      .WithRustDefaults()
                      .WithHttpEndpoint(port: port, name: RustAppExecutableResource.HttpEndpointName, isProxied: false)
                      .WithArgs(allArgs);
    }

    private static IResourceBuilder<RustAppExecutableResource> WithRustDefaults(
        this IResourceBuilder<RustAppExecutableResource> builder) =>
        builder.WithOtlpExporter();
}

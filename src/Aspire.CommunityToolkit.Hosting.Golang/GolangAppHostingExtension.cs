using Aspire.CommunityToolkit.Hosting.Golang.Utils;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.CommunityToolkit.Hosting.Golang;

/// <summary>
/// Provides extension methods for adding Golang applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class GolangAppHostingExtension
{
    /// <summary>
    /// Adds a Golang application to the application model. Executes the executable Golang app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="options">The <see cref="GolangAppExecutableResourceOptions"/> to configure the Golang application.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, string name, string workingDirectory, GolangAppExecutableResourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        string[] allArgs = options.Args is { Length: > 0 }
            ? ["run", ".", .. options.Args] 
            : ["run", ".",];

        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));
        var resource = new GolangAppExecutableResource(name, "go", workingDirectory);

        return builder.AddResource(resource)
                      .WithGolangDefaults()
                      .WithHttpEndpoint(port: options.Port, name: GolangAppExecutableResource.HttpEndpointName, isProxied: false)
                      .WithArgs(allArgs);
    }

    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, string name, string workingDirectory, int port = 8080, string[]? args = null) =>
        builder.AddGolangApp(name, workingDirectory, new GolangAppExecutableResourceOptions { Port = port, Args = args });

    private static IResourceBuilder<GolangAppExecutableResource> WithGolangDefaults(
        this IResourceBuilder<GolangAppExecutableResource> builder) =>
        builder.WithOtlpExporter();
}

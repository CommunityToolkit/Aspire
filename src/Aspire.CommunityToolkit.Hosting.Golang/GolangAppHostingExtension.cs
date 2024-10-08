using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

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
    /// <param name="port">This is the port that will be given to other resource to communicate with this resource.</param>"
    /// <param name="args">The optinal arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, string name, string workingDirectory, int port = 8080, string[]? args = null) 
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        string[] allArgs = args is { Length: > 0 }
            ? ["run", ".", .. args] 
            : ["run", ".",];

        workingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory);
        var resource = new GolangAppExecutableResource(name, workingDirectory);

        return builder.AddResource(resource)
                      .WithGolangDefaults()
                      .WithHttpEndpoint(port: port, name: GolangAppExecutableResource.HttpEndpointName, isProxied: false)
                      .WithArgs(allArgs);
    }

    private static IResourceBuilder<GolangAppExecutableResource> WithGolangDefaults(
        this IResourceBuilder<GolangAppExecutableResource> builder) =>
        builder.WithOtlpExporter();
}

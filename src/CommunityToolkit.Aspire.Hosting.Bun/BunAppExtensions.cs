using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding a Bun app to a <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class BunAppExtensions
{
    /// <summary>
    /// Adds a Bun app to the builder.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory.</param>
    /// <param name="entryPoint">The entry point, either a file or package.json script name.</param>
    /// <param name="watch">Whether to watch for changes.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<BunAppResource> AddBunApp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string? workingDirectory = null,
        string entryPoint = "index.ts",
        bool watch = false)
    {
        var resource = new BunAppResource(name, workingDirectory ?? Path.Combine("..", name));

        return builder.AddResource(resource)
            .WithBunDefaults()
            .WithArgs([watch ? "--watch" : "", "run", entryPoint]);
    }

    private static IResourceBuilder<BunAppResource> WithBunDefaults(
        this IResourceBuilder<BunAppResource> builder) => builder
            .WithEnvironment("NODE_ENV", builder.ApplicationBuilder.Environment.IsDevelopment() ? "development" : "production")
            .WithOtlpExporter();
}

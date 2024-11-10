using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Golang.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Uvicorn applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class UvicornAppHostingExtension
{
    /// <summary>
    /// Adds a Uvicorn application to the distributed application builder.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Uvicorn application.</param>
    /// <param name="projectDirectory">The directory of the project containing the Uvicorn application.</param>
    /// <param name="appName">The name of the uvicorn app.</param>
    /// <param name="scriptArgs">Optional arguments to pass to the script.</param>
    /// <returns>An <see cref="IResourceBuilder{UvicornAppResource}"/> for the Uvicorn application resource.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is null.</exception>
    public static IResourceBuilder<UvicornAppResource> AddUvicornApp(this IDistributedApplicationBuilder builder, string name, string projectDirectory, string appName, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(appName);

        string wd = projectDirectory ?? Path.Combine("..", name);

        projectDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        string[] allArgs = args is { Length: > 0 }
            ? [appName, .. args]
            : [appName];

        var projectResource = new UvicornAppResource(name, projectDirectory);

        var resourceBuilder = builder.AddResource(projectResource)
            .WithArgs(allArgs);

        return resourceBuilder;
    }
}
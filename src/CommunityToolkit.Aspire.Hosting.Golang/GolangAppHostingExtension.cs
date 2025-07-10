using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

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
    /// <param name="args">The optinal arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete("Use AddGolangApp with buildTags parameter instead. This method will be removed in a future version.")]
    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string[] args)
        => AddGolangApp(builder, name, workingDirectory, args, null);

    /// <summary>
    /// Adds a Golang application to the application model. Executes the executable Golang app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="args">The optinal arguments to be passed to the executable when it is started.</param>
    /// <param name="buildTags">The optional build tags to be used when building the Golang application.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string[]? args = null, string[]? buildTags = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        var allArgs = new List<string> { "run" };

        if (buildTags is { Length: > 0 })
        {
            allArgs.Add("-tags");
            allArgs.Add(string.Join(",", buildTags));
        }

        allArgs.Add(".");

        if (args is { Length: > 0 })
        {
            allArgs.AddRange(args);
        }

        workingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        var resource = new GolangAppExecutableResource(name, workingDirectory);

        return builder.AddResource(resource)
                      .WithGolangDefaults()
                      .WithArgs([.. allArgs]);
    }

    private static IResourceBuilder<GolangAppExecutableResource> WithGolangDefaults(
        this IResourceBuilder<GolangAppExecutableResource> builder) =>
        builder.WithOtlpExporter();
}

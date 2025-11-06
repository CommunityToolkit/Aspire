using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Streamlit applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class StreamlitAppHostingExtension
{
    /// <summary>
    /// Adds a Streamlit application to the distributed application builder.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Streamlit application.</param>
    /// <param name="projectDirectory">The directory of the project containing the Streamlit application.</param>
    /// <param name="scriptPath">The path to the Python script to be run by Streamlit.</param>
    /// <param name="args">Optional arguments to pass to the Streamlit command.</param>
    /// <returns>An <see cref="IResourceBuilder{StreamlitAppResource}"/> for the Streamlit application resource.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> or <paramref name="scriptPath"/> is null or empty.</exception>
    public static IResourceBuilder<StreamlitAppResource> AddStreamlitApp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string projectDirectory,
        string scriptPath,
        params string[] args)
    {
        return builder.AddStreamlitApp(name, projectDirectory, scriptPath, ".venv", args);
    }

    private static IResourceBuilder<StreamlitAppResource> AddStreamlitApp(
        this IDistributedApplicationBuilder builder,
        string name,
        string projectDirectory,
        string scriptPath,
        string virtualEnvironmentPath,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        string wd = projectDirectory ?? Path.Combine("..", name);

        var normalizedAppHostDirectory = PathNormalizer.NormalizePathForCurrentPlatform(builder.AppHostDirectory);
        var normalizedWd = PathNormalizer.NormalizePathForCurrentPlatform(wd);
        projectDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(normalizedAppHostDirectory, normalizedWd));

        var projectResource = new StreamlitAppResource(name, projectDirectory);

        return builder.AddResource(projectResource)
            .WithEnvironment(context =>
            {
                // Streamlit uses STREAMLIT_SERVER_PORT instead of PORT, so map PORT to STREAMLIT_SERVER_PORT
                if (context.EnvironmentVariables.TryGetValue("PORT", out var portValue))
                {
                    context.EnvironmentVariables["STREAMLIT_SERVER_PORT"] = portValue;
                }
            })
            .WithArgs(context =>
            {
                AddProjectArguments(scriptPath, args, context);
            });
    }

    private static void AddProjectArguments(string scriptPath, string[] scriptArgs, CommandLineArgsCallbackContext context)
    {
        context.Args.Add("run");
        context.Args.Add(scriptPath);

        // Add --server.headless to run without browser opening
        context.Args.Add("--server.headless");
        context.Args.Add("true");

        foreach (var arg in scriptArgs)
        {
            context.Args.Add(arg);
        }
    }
}

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Python;
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

        projectDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        var virtualEnvironment = new VirtualEnvironment(Path.IsPathRooted(virtualEnvironmentPath)
            ? virtualEnvironmentPath
            : Path.Join(projectDirectory, virtualEnvironmentPath));

        var instrumentationExecutable = virtualEnvironment.GetExecutable("opentelemetry-instrument");
        var streamlitExecutable = virtualEnvironment.GetExecutable("streamlit") ?? "streamlit";
        var projectExecutable = instrumentationExecutable ?? streamlitExecutable;

        var projectResource = new StreamlitAppResource(name, projectExecutable, projectDirectory);

        var resourceBuilder = builder.AddResource(projectResource).WithArgs(context =>
        {
            // If the project is to be automatically instrumented, add the instrumentation executable arguments first.
            if (!string.IsNullOrEmpty(instrumentationExecutable))
            {
                AddOpenTelemetryArguments(context);

                // Add the streamlit executable as the next argument so we can run the project.
                context.Args.Add("streamlit");
            }

            AddProjectArguments(scriptPath, args, context);
        });

        if (!string.IsNullOrEmpty(instrumentationExecutable))
        {
            resourceBuilder.WithOtlpExporter();

            // Make sure to attach the logging instrumentation setting, so we can capture logs.
            // Without this you'll need to configure logging yourself. Which is kind of a pain.
            resourceBuilder.WithEnvironment("OTEL_PYTHON_LOGGING_AUTO_INSTRUMENTATION_ENABLED", "true");
        }

        return resourceBuilder;
    }

    private static void AddProjectArguments(string scriptPath, string[] scriptArgs, CommandLineArgsCallbackContext context)
    {
        context.Args.Add("run");
        context.Args.Add(scriptPath);

        foreach (var arg in scriptArgs)
        {
            context.Args.Add(arg);
        }
    }

    private static void AddOpenTelemetryArguments(CommandLineArgsCallbackContext context)
    {
        context.Args.Add("--traces_exporter");
        context.Args.Add("otlp");

        context.Args.Add("--logs_exporter");
        context.Args.Add("console,otlp");

        context.Args.Add("--metrics_exporter");
        context.Args.Add("otlp");
    }
}

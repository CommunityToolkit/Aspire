using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Python;
using CommunityToolkit.Aspire.Utils;

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
    /// <param name="args">Optional arguments to pass to the script.</param>
    /// <returns>An <see cref="IResourceBuilder{UvicornAppResource}"/> for the Uvicorn application resource.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is null.</exception>
    public static IResourceBuilder<UvicornAppResource> AddUvicornApp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string projectDirectory,
        string appName,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddUvicornApp(name, projectDirectory, appName, ".venv", args);
    }

    private static IResourceBuilder<UvicornAppResource> AddUvicornApp(this IDistributedApplicationBuilder builder,
        string name,
        string projectDirectory,
        string appName,
        string virtualEnvironmentPath,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(appName);

        string wd = projectDirectory ?? Path.Combine("..", name);

        projectDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, wd));

        var virtualEnvironment = new VirtualEnvironment(Path.IsPathRooted(virtualEnvironmentPath)
            ? virtualEnvironmentPath
            : Path.Join(projectDirectory, virtualEnvironmentPath));

        var instrumentationExecutable = virtualEnvironment.GetExecutable("opentelemetry-instrument");
        var uvicornExecutable = virtualEnvironment.GetExecutable("uvicorn") ?? "uvicorn";
        var projectExecutable = instrumentationExecutable ?? uvicornExecutable;

        var projectResource = new UvicornAppResource(name, projectExecutable, projectDirectory);

        var resourceBuilder = builder.AddResource(projectResource).WithArgs(context =>
        {
            // If the project is to be automatically instrumented, add the instrumentation executable arguments first.
            if (!string.IsNullOrEmpty(instrumentationExecutable))
            {
                AddOpenTelemetryArguments(context);

                // Add the uvicorn executable as the next argument so we can run the project.
                context.Args.Add("uvicorn");
            }

            AddProjectArguments(appName, args, context);
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
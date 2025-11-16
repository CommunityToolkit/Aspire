using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Python;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Streamlit applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class StreamlitAppHostingExtension
{
    /// <summary>
    /// Adds a Streamlit application to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the Streamlit application.</param>
    /// <param name="appDirectory">The path to the directory containing the Streamlit application.</param>
    /// <param name="scriptPath">The path to the Python script to be run by Streamlit (relative to appDirectory).</param>
    /// <returns>An <see cref="IResourceBuilder{StreamlitAppResource}"/> for the Streamlit application resource.</returns>
    /// <remarks>
    /// <para>
    /// This method uses the Aspire.Hosting.Python integration to run Streamlit applications.
    /// By default, it uses the <c>.venv</c> virtual environment in the app directory.
    /// Use standard Python extension methods like <c>WithVirtualEnvironment</c>, <c>WithPip</c>, or <c>WithUv</c> to customize the environment.
    /// </para>
    /// <para>
    /// **⚠️ EXPERIMENTAL:** This integration is experimental and subject to change. The underlying implementation
    /// will be updated to use public APIs when they become available in Aspire.Hosting.Python (expected in Aspire 13.1).
    /// </para>
    /// </remarks>
    /// <example>
    /// Add a Streamlit application to the application model:
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddStreamlitApp("dashboard", "../streamlit-app", "app.py")
    ///        .WithHttpEndpoint(env: "PORT");
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    [Experimental("CTASPIRE001", UrlFormat = "https://github.com/CommunityToolkit/Aspire/issues/{0}")]
    public static IResourceBuilder<StreamlitAppResource> AddStreamlitApp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string appDirectory,
        string scriptPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(appDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        // Use AddPythonExecutable to run streamlit from the virtual environment
        var pythonBuilder = builder.AddPythonExecutable(name, appDirectory, "streamlit")
            .WithDebugging()
            .WithHttpEndpoint(env: "PORT")
            .WithArgs(context =>
            {
                context.Args.Add("run");
                context.Args.Add(scriptPath);

                // Add --server.headless to run without browser opening
                context.Args.Add("--server.headless");
                context.Args.Add("true");

                // Configure server port from endpoint
                var endpoint = ((IResourceWithEndpoints)context.Resource).GetEndpoint("http");
                context.Args.Add("--server.port");
                context.Args.Add(endpoint.Property(EndpointProperty.TargetPort));

                // Configure server address
                context.Args.Add("--server.address");
                if (builder.ExecutionContext.IsPublishMode)
                {
                    context.Args.Add("0.0.0.0");
                }
                else
                {
                    context.Args.Add(endpoint.EndpointAnnotation.TargetHost);
                }
            });

        // Create a StreamlitAppResource wrapping the PythonAppResource
        // This allows for Streamlit-specific extension methods in the future
        var streamlitResource = new StreamlitAppResource(
            pythonBuilder.Resource.Name,
            pythonBuilder.Resource.Command,
            pythonBuilder.Resource.WorkingDirectory);

        // Copy annotations from the Python resource
        foreach (var annotation in pythonBuilder.Resource.Annotations)
        {
            streamlitResource.Annotations.Add(annotation);
        }

        // Replace the resource in the builder
        builder.Resources.Remove(pythonBuilder.Resource);
        var streamlitBuilder = builder.AddResource(streamlitResource);

        return streamlitBuilder;
    }
}

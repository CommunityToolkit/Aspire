using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Java applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static partial class JavaAppHostingExtension
{
    /// <summary>
    /// Adds a Java application to the application model. Executes the containerized Java app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="image">The container image name.</param>
    /// <param name="imageTag">The container image tag.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppContainerResource> AddJavaContainerApp(this IDistributedApplicationBuilder builder, [ResourceName] string name,
        string image, string? imageTag = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(image, nameof(image));

        var resource = new JavaAppContainerResource(name);

        return builder.AddResource(resource)
                      .WithImage(image, imageTag)
                      .WithOtelAgent();
    }

    /// <summary>
    /// Adds a Java application to the application model. Executes the containerized Java app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">The <see cref="JavaAppContainerResourceOptions"/> to configure the Java application.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete("Use AddJavaContainerApp instead. This method will be removed in a future version.")]
    public static IResourceBuilder<JavaAppContainerResource> AddJavaApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, JavaAppContainerResourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContainerImageName, nameof(options.ContainerImageName));

        var resource = new JavaAppContainerResource(name);

        var rb = builder.AddResource(resource)
          .WithAnnotation(new ContainerImageAnnotation { Image = options.ContainerImageName, Tag = options.ContainerImageTag, Registry = options.ContainerRegistry })
          .WithHttpEndpoint(port: options.Port, targetPort: options.TargetPort, name: JavaAppContainerResource.HttpEndpointName)
          .WithJavaDefaults(options);

        if (options.Args is { Length: > 0 })
        {
            rb.WithArgs(options.Args);
        }

        return rb;
    }


    /// <summary>
    /// Adds a Spring application to the application model. Executes the containerized Spring app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="options">The <see cref="JavaAppContainerResourceOptions"/> to configure the Java application.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete("Use AddJavaApp instead. This method will be removed in a future version.")]
    public static IResourceBuilder<JavaAppContainerResource> AddSpringApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, JavaAppContainerResourceOptions options) =>
        builder.AddJavaApp(name, options);

#pragma warning disable CS0618
    private static IResourceBuilder<JavaAppContainerResource> WithJavaDefaults(
        this IResourceBuilder<JavaAppContainerResource> builder,
        JavaAppContainerResourceOptions options) =>
        builder.WithOtlpExporter()
               .WithEnvironment("JAVA_TOOL_OPTIONS", $"-javaagent:{options.OtelAgentPath?.TrimEnd('/')}/opentelemetry-javaagent.jar");
#pragma warning restore CS0618
}

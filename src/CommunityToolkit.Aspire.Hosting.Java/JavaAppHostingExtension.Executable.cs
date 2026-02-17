using System.Globalization;
using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Java applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static partial class JavaAppHostingExtension
{
    /// <summary>
    /// Adds a Java application to the application model. Executes the executable Java app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="jarPath">The path to the jar file to execute. Default is <c>target/app.jar</c>.</param>
    /// <param name="args">The optional arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppExecutableResource> AddJavaApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory,
        string? jarPath = null, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));

        var resource = new JavaAppExecutableResource(name, workingDirectory)
        {
            JarPath = jarPath ?? "target/app.jar"
        };

        return builder.AddResource(resource)
                      .WithOtelAgent()
                      .WithArgs(context =>
                      {
                          context.Args.Add("-jar");
                          context.Args.Add(resource.JarPath);

                          if (args is { Length: > 0 })
                          {
                              foreach (var arg in args)
                              {
                                  context.Args.Add(arg);
                              }
                          }
                      });
    }

    /// <summary>
    /// Configures the Java application to be published as a Dockerfile with automatic multi-stage build generation.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="workingDirectory">The working directory containing the Java application.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
#pragma warning disable ASPIREDOCKERFILEBUILDER001
    private static IResourceBuilder<JavaAppExecutableResource> PublishAsJavaDockerfile(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        string workingDirectory)
    {
        return builder.PublishAsDockerFile(publish =>
        {
            publish.WithDockerfileBuilder(workingDirectory, context =>
            {
                if (!builder.Resource.TryGetLastAnnotation<JavaBuildAnnotation>(out var buildAnnotation))
                {
                    buildAnnotation = new JavaBuildAnnotation("eclipse-temurin:25-jdk-alpine", "./mvnw clean package -DskipTests");
                }

                var buildStage = context.Builder
                    .From(buildAnnotation.BuildImage, "builder")
                    .WorkDir("/build")
                    .Copy(".", "./")
                    .Run(buildAnnotation.BuildCommand);

                var logger = context.Services.GetService<ILogger<JavaAppExecutableResource>>() ?? NullLogger<JavaAppExecutableResource>.Instance;

                context.Builder
                    .From("eclipse-temurin:25-jre-alpine")
                    .Run("apk --no-cache add ca-certificates")
                    .WorkDir("/app")
                    .CopyFrom(buildStage.StageName!, $"/build/{builder.Resource.JarPath}", "/app/app.jar")
                    .Entrypoint(["java", "-jar", "/app/app.jar"]);
            });
        });
    }
#pragma warning restore ASPIREDOCKERFILEBUILDER001

    /// <summary>
    /// Adds a Java application to the application model. Executes the executable Java app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="options">The <see cref="JavaAppExecutableResourceOptions"/> to configure the Java application.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete("Use AddJavaApp(string, string, string?, string[]?, string[]?) instead. This method will be removed in a future version.")]
    public static IResourceBuilder<JavaAppExecutableResource> AddJavaApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, JavaAppExecutableResourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

#pragma warning disable CS8601 // Possible null reference assignment.
        string[] allArgs = options.Args is { Length: > 0 }
            ? ["-jar", options.ApplicationName, .. options.Args]
            : ["-jar", options.ApplicationName];
#pragma warning restore CS8601 // Possible null reference assignment.

        if (options.JvmArgs is { Length: > 0 })
            allArgs = [.. options.JvmArgs, .. allArgs];

        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));
        var resource = new JavaAppExecutableResource(name, workingDirectory);

        return builder.AddResource(resource)
                      .WithJavaDefaults(otelAgentPath: options.OtelAgentPath, port: options.Port)
                      .WithHttpEndpoint(port: options.Port, name: JavaAppContainerResource.HttpEndpointName, isProxied: false)
                      .WithArgs(allArgs);
    }

    /// <summary>
    /// Adds a Spring application to the application model. Executes the executable Spring app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="options">The <see cref="JavaAppExecutableResourceOptions"/> to configure the Java application.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete("Use AddJavaApp instead. This method will be removed in a future version.")]
    public static IResourceBuilder<JavaAppExecutableResource> AddSpringApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, JavaAppExecutableResourceOptions options) => builder.AddJavaApp(name, workingDirectory, options);


    /// <summary>
    /// Adds a Maven build step to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to add the Maven build step to.</param>
    /// <param name="jarPath">The path to the jar file to execute. If not provided, it will be automatically detected in the 'target' directory.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithMavenBuild<T>(
        this IResourceBuilder<T> builder,
        string? jarPath = null) where T : ExecutableResource
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        if (jarPath != null && builder.Resource is JavaAppExecutableResource executableResource)
        {
            executableResource.JarPath = jarPath;
        }

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            var buildResourceName = $"{builder.Resource.Name}-maven-build";
            var buildResource = new MavenBuildResource(buildResourceName, builder.Resource.WorkingDirectory);

            var mavenBuilder = builder.ApplicationBuilder.AddResource(buildResource)
                .WithParentRelationship(builder.Resource)
                .ExcludeFromManifest();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mavenBuilder.WithCommand("cmd");
            }
            else
            {
                mavenBuilder.WithCommand(Path.Combine(builder.Resource.WorkingDirectory, "mvnw"));
            }

            mavenBuilder.WithArgs(context =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    context.Args.Add("/c");
                    context.Args.Add("mvnw.cmd");
                }

                context.Args.Add("clean");
                context.Args.Add("package");
            });

            builder.WaitForCompletion(mavenBuilder);
        }

        if (builder.Resource is JavaAppExecutableResource javaAppExecutableResource)
        {
            builder.ApplicationBuilder.CreateResourceBuilder(javaAppExecutableResource)
                .PublishAsJavaDockerfile(builder.Resource.WorkingDirectory);
        }

        return builder;
    }

    /// <summary>
    /// Adds a Gradle build step to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to add the Gradle build step to.</param>
    /// <param name="jarPath">The path to the executable jar.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithGradleBuild<T>(
        this IResourceBuilder<T> builder,
        string? jarPath = null) where T : ExecutableResource
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        if (jarPath != null && builder.Resource is JavaAppExecutableResource executableResource)
        {
            executableResource.JarPath = jarPath;
        }

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            var buildResourceName = $"{builder.Resource.Name}-gradle-build";
            var buildResource = new GradleBuildResource(buildResourceName, builder.Resource.WorkingDirectory);

            var gradleBuilder = builder.ApplicationBuilder.AddResource(buildResource)
                .WithParentRelationship(builder.Resource)
                .ExcludeFromManifest();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                gradleBuilder.WithCommand("cmd");
            }
            else
            {
                gradleBuilder.WithCommand(Path.Combine(builder.Resource.WorkingDirectory, "gradlew"));
            }

            gradleBuilder.WithArgs(context =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    context.Args.Add("/c");
                    context.Args.Add("gradlew.bat");
                }

                context.Args.Add("clean");
                context.Args.Add("build");
            });

            builder.WaitForCompletion(gradleBuilder);
        }

        if (builder.Resource is JavaAppExecutableResource javaAppExecutableResource)
        {
            builder.ApplicationBuilder.CreateResourceBuilder(javaAppExecutableResource)
                .PublishAsJavaDockerfile(builder.Resource.WorkingDirectory);
        }

        return builder;
    }

    /// <summary>
    /// Configures the Java Virtual Machine arguments for the Java application.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="args">The JVM arguments.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppExecutableResource> WithJvmArgs(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithArgs(context =>
        {
            foreach (var arg in args)
            {
                context.Args.Insert(0, arg);
            }
        });
    }

    /// <summary>
    /// Configures the OpenTelemetry Java Agent for the Java application.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="agentPath">The path to the OpenTelemetry Java Agent jar file.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithOtelAgent<T>(
        this IResourceBuilder<T> builder,
        string? agentPath = null) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.WithOtlpExporter();

        if (!string.IsNullOrEmpty(agentPath))
        {
            builder.WithEnvironment("JAVA_TOOL_OPTIONS", $"-javaagent:{agentPath}");
        }

        return builder;
    }

    [Obsolete("Use WithOtelAgent instead.")]
    private static IResourceBuilder<T> WithJavaDefaults<T>(
        this IResourceBuilder<T> builder,
        string? otelAgentPath = null,
        int? port = null) where T : IResourceWithEnvironment
    {
        builder.WithOtelAgent($"{otelAgentPath?.TrimEnd('/')}/opentelemetry-javaagent.jar");

        if (port.HasValue)
        {
            builder.WithEnvironment("SERVER_PORT", port.Value.ToString(CultureInfo.InvariantCulture));
        }

        return builder;
    }
}

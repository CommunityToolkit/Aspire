using System.Globalization;
using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
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
    /// <param name="jarPath">The path to the jar file, relative to the resource working directory.</param>
    /// <param name="args">The optional arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppExecutableResource> AddJavaApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory,
        string jarPath, string[]? args = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));
        ArgumentException.ThrowIfNullOrWhiteSpace(jarPath, nameof(jarPath));

        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));

        var resource = new JavaAppExecutableResource(name, workingDirectory)
        {
            JarPath = jarPath
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
                    buildAnnotation = new JavaBuildAnnotation("eclipse-temurin:21-jdk-alpine", "./mvnw clean package -DskipTests");
                }

                var buildStage = context.Builder
                    .From(buildAnnotation.BuildImage, "builder")
                    .WorkDir("/build")
                    .Copy(".", "./")
                    .Run(buildAnnotation.BuildCommand);

                var logger = context.Services.GetService<ILogger<JavaAppExecutableResource>>() ?? NullLogger<JavaAppExecutableResource>.Instance;

                context.Builder
                    .From("eclipse-temurin:21-jre-alpine")
                    .Run("apk --no-cache add ca-certificates")
                    .WorkDir("/app")
                    .CopyFrom(buildStage.StageName!, $"/build/{builder.Resource.JarPath}", "/app/app.jar")
                    .AddContainerFiles(context.Resource, "/app", logger)
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
    [Obsolete("Use AddJavaApp(string, string, string, string[]?) instead. This method will be removed in a future version.")]
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
        var resource = new JavaAppExecutableResource(name, "java", workingDirectory);

        return builder.AddResource(resource)
                      .WithJavaDefaults(options)
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
    public static IResourceBuilder<JavaAppExecutableResource> AddSpringApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, JavaAppExecutableResourceOptions options) =>
        builder.AddJavaApp(name, workingDirectory, options);

    /// <summary>
    /// Adds a Maven build step to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to add the Maven build step to.</param>
    /// <param name="mavenOptions">The <see cref="MavenOptions"/> to configure the Maven build step.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete("Use WithMavenBuild(string?, params string[]) instead. This method will be removed in a future version.")]
    public static IResourceBuilder<JavaAppExecutableResource> WithMavenBuild(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        MavenOptions mavenOptions)
    {
        ArgumentNullException.ThrowIfNull(mavenOptions, nameof(mavenOptions));

        return builder.WithMavenBuild(args: mavenOptions.Args);
    }


    /// <summary>
    /// Adds a Maven build step to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to add the Maven build step to.</param>
    /// <param name="wrapperScript">The path to the Maven wrapper script, relative to the resource working directory. If not provided, defaults to <c>mvnw</c> (or <c>mvnw.cmd</c> on Windows) in the working directory.</param>
    /// <param name="args">Arguments to pass to the Maven wrapper. If not provided, defaults to <c>clean package</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppExecutableResource> WithMavenBuild(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        string? wrapperScript = null,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : string.Empty;

        wrapperScript ??= $"mvnw{extension}";

        string wrapperPath = Path.GetFullPath(Path.Combine(builder.Resource.WorkingDirectory, wrapperScript));

        return builder.WithJavaBuildStep(
            buildResourceName: $"{builder.Resource.Name}-maven-build",
            createResource: (name, workingDirectory) => new MavenBuildResource(name, workingDirectory),
            wrapperPath: wrapperPath,
            buildArgs: args.Length > 0 ? args : ["clean", "package"]);
    }

    /// <summary>
    /// Adds a Gradle build step to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to add the Gradle build step to.</param>
    /// <param name="wrapperScript">The path to the Gradle wrapper script, relative to the resource working directory. If not provided, defaults to <c>gradlew</c> (or <c>gradlew.bat</c> on Windows) in the working directory.</param>
    /// <param name="args">Arguments to pass to the Gradle wrapper. If not provided, defaults to <c>clean build</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppExecutableResource> WithGradleBuild(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        string? wrapperScript = null,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".bat" : string.Empty;
        
        wrapperScript ??= $"gradlew{extension}";

        string wrapperPath = Path.GetFullPath(Path.Combine(builder.Resource.WorkingDirectory, wrapperScript));

        return builder.WithJavaBuildStep(
            buildResourceName: $"{builder.Resource.Name}-gradle-build",
            createResource: (name, workingDirectory) => new GradleBuildResource(name, workingDirectory),
            wrapperPath: wrapperPath,
            buildArgs: args.Length > 0 ? args : ["clean", "build"]);
    }

    private static IResourceBuilder<JavaAppExecutableResource> WithJavaBuildStep<TBuildResource>(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        string buildResourceName,
        Func<string, string, TBuildResource> createResource,
        string wrapperPath,
        string[] buildArgs) where TBuildResource : ExecutableResource
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            var buildResource = createResource(buildResourceName, builder.Resource.WorkingDirectory);

            var buildBuilder = builder.ApplicationBuilder.AddResource(buildResource)
                .WithCommand(wrapperPath)
                .WithArgs(buildArgs)
                .WithParentRelationship(builder.Resource)
                .ExcludeFromManifest();

            builder.WaitForCompletion(buildBuilder);
        }

        // Use the file name only for the Dockerfile RUN command, since the wrapper
        // is executed relative to the container's WORKDIR, not the host machine.
        string wrapperFileName = Path.GetFileName(wrapperPath);
        string buildCommand = $"./{wrapperFileName} {string.Join(' ', buildArgs)}";
        builder.Resource.Annotations.Add(
            new JavaBuildAnnotation("eclipse-temurin:21-jdk-alpine", buildCommand));
        builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource)
            .PublishAsJavaDockerfile(builder.Resource.WorkingDirectory);

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
            for (int i = args.Length - 1; i >= 0; i--)
            {
                context.Args.Insert(0, args[i]);
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
    private static IResourceBuilder<JavaAppExecutableResource> WithJavaDefaults(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        JavaAppExecutableResourceOptions options) =>
        builder.WithOtlpExporter()
               .WithEnvironment("JAVA_TOOL_OPTIONS", $"-javaagent:{options.OtelAgentPath?.TrimEnd('/')}/opentelemetry-javaagent.jar")
               .WithEnvironment("SERVER_PORT", options.Port.ToString(CultureInfo.InvariantCulture));
}

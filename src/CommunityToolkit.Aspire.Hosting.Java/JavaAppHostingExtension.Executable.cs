using System.Globalization;
using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Java applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static partial class JavaAppHostingExtension
{
    private const string JavaToolOptions = "JAVA_TOOL_OPTIONS";

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

        var rb = builder.AddJavaApp(name, workingDirectory);
        rb.Resource.JarPath = jarPath;

        if (args is { Length: > 0 })
        {
            rb.WithArgs(args);
        }

        return rb;
    }

    /// <summary>
    /// Adds a Java application to the application model. Executes the executable Java app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// Use <see cref="WithMavenGoal"/> or <see cref="WithGradleTask"/> to run the application via a build tool,
    /// or use the overload that accepts a <c>jarPath</c> parameter to run with <c>java -jar</c>.
    /// </remarks>
    public static IResourceBuilder<JavaAppExecutableResource> AddJavaApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        workingDirectory = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(builder.AppHostDirectory, workingDirectory));

        var resource = new JavaAppExecutableResource(name, workingDirectory);

        var resourceBuilder = builder.AddResource(resource)
                      .WithOtelAgent()
                      .WithArgs(context =>
                      {
                          if (resource.TryGetLastAnnotation<JavaBuildToolAnnotation>(out var buildTool))
                          {
                              foreach (var arg in buildTool.Args)
                              {
                                  context.Args.Add(arg);
                              }
                          }
                          else if (resource.JarPath is not null)
                          {
                              context.Args.Add("-jar");
                              context.Args.Add(resource.JarPath);
                          }
                      });

        if (builder.ExecutionContext.IsRunMode)
        {
            builder.Eventing.Subscribe<BeforeStartEvent>((_, _) =>
            {
                if (resource.TryGetLastAnnotation<JavaBuildToolAnnotation>(out var buildTool))
                {
                    resourceBuilder.WithCommand(buildTool.WrapperPath);
                }

                return Task.CompletedTask;
            });
        }

        return resourceBuilder;
    }

    /// <summary>
    /// Adds a Java application to the application model. Executes the executable Java app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="options">The <see cref="JavaAppExecutableResourceOptions"/> to configure the Java application.</param>
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
    /// <param name="options">The <see cref="JavaAppExecutableResourceOptions"/> to configure the Java application.</param>
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
            createResource: (name, wrapperScript, workingDirectory) => new MavenBuildResource(name, wrapperScript, workingDirectory),
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
            createResource: (name, wrapperScript, workingDirectory) => new GradleBuildResource(name, wrapperScript, workingDirectory),
            wrapperPath: wrapperPath,
            buildArgs: args.Length > 0 ? args : ["clean", "build"]);
    }

    private static IResourceBuilder<JavaAppExecutableResource> WithJavaBuildStep<TBuildResource>(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        string buildResourceName,
        Func<string, string, string, TBuildResource> createResource,
        string wrapperPath,
        string[] buildArgs) where TBuildResource : ExecutableResource
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            var buildResource = createResource(buildResourceName, wrapperPath, builder.Resource.WorkingDirectory);

            var buildBuilder = builder.ApplicationBuilder.AddResource(buildResource)
                .WithArgs(buildArgs)
                .WithParentRelationship(builder.Resource)
                .ExcludeFromManifest();

            builder.WaitForCompletion(buildBuilder);
        }

        return builder;
    }

    /// <summary>
    /// Configures the Java application to run using a Maven goal (e.g., <c>spring-boot:run</c>).
    /// In run mode, the resource command is changed from <c>java</c> to the Maven wrapper.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to configure.</param>
    /// <param name="goal">The Maven goal to execute (e.g., <c>spring-boot:run</c>).</param>
    /// <param name="args">Additional arguments to pass to the Maven wrapper.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppExecutableResource> WithMavenGoal(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        string goal,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(goal, nameof(goal));

        string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : string.Empty;
        string wrapperPath = Path.GetFullPath(Path.Combine(builder.Resource.WorkingDirectory, $"mvnw{extension}"));

        builder.Resource.Annotations.Add(
            new JavaBuildToolAnnotation(wrapperPath, args.Length > 0 ? [goal, .. args] : [goal]));

        return builder;
    }

    /// <summary>
    /// Configures the Java application to run using a Gradle task (e.g., <c>bootRun</c>).
    /// In run mode, the resource command is changed from <c>java</c> to the Gradle wrapper.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to configure.</param>
    /// <param name="task">The Gradle task to execute (e.g., <c>bootRun</c>).</param>
    /// <param name="args">Additional arguments to pass to the Gradle wrapper.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<JavaAppExecutableResource> WithGradleTask(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        string task,
        params string[] args)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(task, nameof(task));

        string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".bat" : string.Empty;
        string wrapperPath = Path.GetFullPath(Path.Combine(builder.Resource.WorkingDirectory, $"gradlew{extension}"));

        builder.Resource.Annotations.Add(
            new JavaBuildToolAnnotation(wrapperPath, args.Length > 0 ? [task, .. args] : [task]));

        return builder;
    }

    /// <summary>
    /// Configures the Java Virtual Machine arguments for the Java application.
    /// The arguments are set via the <c>JAVA_TOOL_OPTIONS</c> environment variable,
    /// which is recognized by the JVM regardless of how the application is launched
    /// (e.g., <c>java -jar</c>, Maven wrapper, or Gradle wrapper).
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="args">The JVM arguments.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithJvmArgs<T>(
        this IResourceBuilder<T> builder,
        params string[] args) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithEnvironment(context =>
        {
            AppendJavaToolOptions(context, args);
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
            builder.WithEnvironment(context =>
            {
                AppendJavaToolOptions(context, [$"-javaagent:{agentPath}"]);
            });
        }

        return builder;
    }

    /// <summary>
    /// Merges the specified values into the <c>JAVA_TOOL_OPTIONS</c> environment variable. 
    /// This ensures that all JVM arguments are passed to the Java application regardless of how it is launched.
    /// </summary>
    /// <param name="context">The environment callback context.</param>
    /// <param name="values">The values to append.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    private static void AppendJavaToolOptions(EnvironmentCallbackContext context, string[] values)
    {
        var value = string.Join(' ', values);

        if (context.EnvironmentVariables.TryGetValue(JavaToolOptions, out var existing) &&
            existing is string existingValue &&
            !string.IsNullOrEmpty(existingValue))
        {
            context.EnvironmentVariables[JavaToolOptions] = $"{existingValue} {value}";
        }
        else
        {
            context.EnvironmentVariables[JavaToolOptions] = value;
        }
    }

    [Obsolete("Use WithOtelAgent instead.")]
    private static IResourceBuilder<JavaAppExecutableResource> WithJavaDefaults(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        JavaAppExecutableResourceOptions options) =>
        builder.WithOtlpExporter()
               .WithEnvironment(JavaToolOptions, $"-javaagent:{options.OtelAgentPath?.TrimEnd('/')}/opentelemetry-javaagent.jar")
               .WithEnvironment("SERVER_PORT", options.Port.ToString(CultureInfo.InvariantCulture));
}

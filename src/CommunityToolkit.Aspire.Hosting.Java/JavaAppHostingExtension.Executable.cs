using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <param name="options">The <see cref="JavaAppExecutableResourceOptions"/> to configure the Java application.</param>"
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
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
    public static IResourceBuilder<JavaAppExecutableResource> AddSpringApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, JavaAppExecutableResourceOptions options) =>
        builder.AddJavaApp(name, workingDirectory, options);

    /// <summary>
    /// Adds a Maven build step to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to add the Maven build step to.</param>
    /// <param name="mavenOptions">The <see cref="MavenOptions"/> to configure the Maven build step.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// This method adds a Maven build step to the application model. The Maven build step is executed before the Java application is started.
    /// 
    /// The Maven build step is added as an executable resource named "maven" with the command "mvnw --quiet clean package".
    /// 
    /// The Maven build step is excluded from the manifest file.
    /// </remarks>
    public static IResourceBuilder<JavaAppExecutableResource> WithMavenBuild(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        MavenOptions? mavenOptions = null)
    {
        mavenOptions ??= new MavenOptions();

        mavenOptions.WorkingDirectory ??= builder.Resource.WorkingDirectory;

        return builder.WithJavaBuild(
            new MavenBuildAnnotation(mavenOptions),
            "Maven",
            (resource, services, ct, useNotificationService) =>
                ExecuteBuild(resource, services, ct, useNotificationService, "Maven", mavenOptions));
    }

    /// <summary>
    /// Adds a Gradle build step to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> to add the Gradle build step to.</param>
    /// <param name="gradleOptions">The <see cref="GradleOptions"/> to configure the Gradle build step.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// This method adds a Gradle build step to the application model. The Gradle build step is executed before the Java application is started.
    /// 
    /// The Gradle build step is added as an executable resource named "gradle" with the command "gradlew --quiet clean build".
    /// 
    /// The Gradle build step is excluded from the manifest file.
    /// </remarks>
    public static IResourceBuilder<JavaAppExecutableResource> WithGradleBuild(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        GradleOptions? gradleOptions = null)
    {
        gradleOptions ??= new GradleOptions();

        gradleOptions.WorkingDirectory ??= builder.Resource.WorkingDirectory;

        return builder.WithJavaBuild(
            new GradleBuildAnnotation(gradleOptions),
            "Gradle",
            (resource, services, ct, useNotificationService) =>
                ExecuteBuild(resource, services, ct, useNotificationService, "Gradle", gradleOptions));
    }

    private static IResourceBuilder<JavaAppExecutableResource> WithJavaBuild<TAnnotation>(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        TAnnotation annotation,
        string buildSystemName,
        Func<JavaAppExecutableResource, IServiceProvider, CancellationToken, bool, Task<bool>> buildFunc)
        where TAnnotation : IResourceAnnotation
    {
        if (builder.Resource.TryGetLastAnnotation<TAnnotation>(out _))
        {
            // Replace the existing annotation, but don't continue on and subscribe to the event again.
            builder.WithAnnotation(annotation, ResourceAnnotationMutationBehavior.Replace);
            return builder;
        }

        builder.WithAnnotation(annotation);

        builder.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(builder.Resource, async (e, ct) =>
        {
            if (e.Resource is not JavaAppExecutableResource javaAppResource)
            {
                return;
            }

            await buildFunc(javaAppResource, e.Services, ct, true).ConfigureAwait(false);
        });

        builder.WithCommand(
            $"build-with-{buildSystemName.ToLowerInvariant()}",
            $"Build with {buildSystemName}",
            async (context) =>
                await buildFunc(builder.Resource, context.ServiceProvider, context.CancellationToken, false).ConfigureAwait(false) ?
                    new ExecuteCommandResult { Success = true } :
                    new ExecuteCommandResult { Success = false, ErrorMessage = $"Failed to build with {buildSystemName}" },
            new CommandOptions()
            {
                IconName = "build",
                UpdateState = (context) => context.ResourceSnapshot.State switch
                {
                    { Text: "Stopped" } or
                    { Text: "Exited" } or
                    { Text: "Finished" } or
                    { Text: "FailedToStart" } => ResourceCommandState.Enabled,
                    _ => ResourceCommandState.Disabled
                },
            });

        return builder;
    }

    private static async Task<bool> ExecuteBuild(
        JavaAppExecutableResource javaAppResource,
        IServiceProvider services,
        CancellationToken ct,
        bool useNotificationService,
        string buildSystemName,
        JavaBuildOptions options)
    {
        var logger = services.GetRequiredService<ResourceLoggerService>().GetLogger(javaAppResource);
        var notificationService = services.GetRequiredService<ResourceNotificationService>();

        if (useNotificationService)
        {
            await notificationService.PublishUpdateAsync(javaAppResource, state => state with
            {
                State = new($"Building {buildSystemName} project", KnownResourceStates.Starting)
            }).ConfigureAwait(false);
        }

        logger.LogInformation("Building {BuildSystemName} project", buildSystemName);

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd" : "sh",
                Arguments = isWindows ? $"/c {options.Command} {string.Join(" ", options.Args)}" : $"./{options.Command} {string.Join(" ", options.Args)}",
                WorkingDirectory = options.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false,
            }
        };

        process.OutputDataReceived += async (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                if (useNotificationService)
                {
                    await notificationService.PublishUpdateAsync(javaAppResource, state => state with
                    {
                        State = new(args.Data, KnownResourceStates.Starting)
                    }).ConfigureAwait(false);
                }

                logger.LogInformation("{Data}", args.Data);
            }
        };

        process.ErrorDataReceived += async (sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                if (useNotificationService)
                {
                    await notificationService.PublishUpdateAsync(javaAppResource, state => state with
                    {
                        State = new(args.Data, KnownResourceStates.FailedToStart)
                    }).ConfigureAwait(false);
                }

                logger.LogError("{Data}", args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            // always use notification service to push out errors in the build
            await notificationService.PublishUpdateAsync(javaAppResource, state => state with
            {
                State = new($"{options.Command} exited with {process.ExitCode}", KnownResourceStates.FailedToStart)
            }).ConfigureAwait(false);

            return false;
        }
        return true;
    }

    private static IResourceBuilder<JavaAppExecutableResource> WithJavaDefaults(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        JavaAppExecutableResourceOptions options) =>
        builder.WithOtlpExporter()
               .WithEnvironment("JAVA_TOOL_OPTIONS", $"-javaagent:{options.OtelAgentPath?.TrimEnd('/')}/opentelemetry-javaagent.jar")
               .WithEnvironment("SERVER_PORT", options.Port.ToString(CultureInfo.InvariantCulture));
}

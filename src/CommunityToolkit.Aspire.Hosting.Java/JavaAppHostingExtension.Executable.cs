using System.Diagnostics;
using System.Globalization;
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
        builder.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(builder.Resource, async (e, ct) =>
        {
            if (e.Resource is not JavaAppExecutableResource javaAppResource)
            {
                return;
            }

            var logger = e.Services.GetRequiredService<ResourceLoggerService>().GetLogger(javaAppResource);
            var notificationService = e.Services.GetRequiredService<ResourceNotificationService>();

            await notificationService.PublishUpdateAsync(javaAppResource, state => state with
            {
                State = new("Building Maven project", KnownResourceStates.Starting)
            }).ConfigureAwait(false);

            logger.LogInformation("Building Maven project");

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            var mvnw = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd" : "sh",
                    Arguments = isWindows ? $"/c {mavenOptions.Command} {string.Join(" ", mavenOptions.Args)}" : $"./{mavenOptions.Command} {string.Join(" ", mavenOptions.Args)}",
                    WorkingDirectory = mavenOptions.WorkingDirectory ?? javaAppResource.WorkingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                }
            };

            mvnw.OutputDataReceived += async (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    await notificationService.PublishUpdateAsync(javaAppResource, state => state with
                    {
                        State = new(args.Data, KnownResourceStates.Starting)
                    }).ConfigureAwait(false);

                    logger.LogInformation("{Data}", args.Data);
                }
            };

            mvnw.ErrorDataReceived += async (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    await notificationService.PublishUpdateAsync(javaAppResource, state => state with
                    {
                        State = new(args.Data, KnownResourceStates.FailedToStart)
                    }).ConfigureAwait(false);

                    logger.LogError("{Data}", args.Data);
                }
            };

            mvnw.Start();
            mvnw.BeginOutputReadLine();
            mvnw.BeginErrorReadLine();

            await mvnw.WaitForExitAsync(ct).ConfigureAwait(false);

            if (mvnw.ExitCode != 0)
            {
                await notificationService.PublishUpdateAsync(javaAppResource, state => state with
                {
                    State = new($"mvnw exited with {mvnw.ExitCode}", KnownResourceStates.FailedToStart)
                }).ConfigureAwait(false);

                throw new InvalidOperationException($"mvnw build failed with exit code {mvnw.ExitCode}");
            }
        });

        return builder;
    }

    private static IResourceBuilder<JavaAppExecutableResource> WithJavaDefaults(
        this IResourceBuilder<JavaAppExecutableResource> builder,
        JavaAppExecutableResourceOptions options) =>
        builder.WithOtlpExporter()
               .WithEnvironment("JAVA_TOOL_OPTIONS", $"-javaagent:{options.OtelAgentPath?.TrimEnd('/')}/opentelemetry-javaagent.jar")
               .WithEnvironment("SERVER_PORT", options.Port.ToString(CultureInfo.InvariantCulture));
}

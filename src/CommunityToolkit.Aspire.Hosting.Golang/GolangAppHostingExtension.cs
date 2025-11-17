using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Utils;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Golang applications to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class GolangAppHostingExtension
{
    /// <summary>
    /// Adds a Golang application to the application model. Executes the executable Golang app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="args">The optinal arguments to be passed to the executable when it is started.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [Obsolete("Use AddGolangApp with buildTags parameter instead. This method will be removed in a future version.")]
    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string[] args)
        => AddGolangApp(builder, name, workingDirectory, args, null);

    /// <summary>
    /// Adds a Golang application to the application model. Executes the executable Golang app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="args">The optinal arguments to be passed to the executable when it is started.</param>
    /// <param name="buildTags">The optional build tags to be used when building the Golang application.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string[]? args = null, string[]? buildTags = null)
        => AddGolangApp(builder, name, workingDirectory, ".", args, buildTags);

    /// <summary>
    /// Adds a Golang application to the application model. Executes the executable Golang app.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
    /// <param name="executable">The path to the Golang package directory or source file to be executed. Use "." to execute the program in the current directory. For example, "./cmd/server".</param>
    /// <param name="args">The optinal arguments to be passed to the executable when it is started.</param>
    /// <param name="buildTags">The optional build tags to be used when building the Golang application.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<GolangAppExecutableResource> AddGolangApp(this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string executable, string[]? args = null, string[]? buildTags = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory, nameof(workingDirectory));

        var allArgs = new List<string> { "run" };

        if (buildTags is { Length: > 0 })
        {
            allArgs.Add("-tags");
            allArgs.Add(string.Join(",", buildTags));
        }

        allArgs.Add(executable);

        if (args is { Length: > 0 })
        {
            allArgs.AddRange(args);
        }

        workingDirectory = Path.Combine(builder.AppHostDirectory, workingDirectory).NormalizePathForCurrentPlatform();
        var resource = new GolangAppExecutableResource(name, workingDirectory);

        return builder.AddResource(resource)
                      .WithGolangDefaults()
                      .WithArgs([.. allArgs])
                      .PublishAsGolangDockerfile(workingDirectory, executable, buildTags);
    }

    private static IResourceBuilder<GolangAppExecutableResource> WithGolangDefaults(
        this IResourceBuilder<GolangAppExecutableResource> builder) =>
        builder.WithOtlpExporter();

    /// <summary>
    /// Configures the Golang application to be published as a Dockerfile with automatic multi-stage build generation.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="workingDirectory">The working directory containing the Golang application.</param>
    /// <param name="executable">The path to the Golang package directory or source file to be executed.</param>
    /// <param name="buildTags">The optional build tags to be used when building the Golang application.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
#pragma warning disable ASPIREDOCKERFILEBUILDER001
    private static IResourceBuilder<GolangAppExecutableResource> PublishAsGolangDockerfile(
        this IResourceBuilder<GolangAppExecutableResource> builder,
        string workingDirectory,
        string executable,
        string[]? buildTags)
    {
        const string DefaultAlpineVersion = "3.21";

        return builder.PublishAsDockerFile(publish =>
        {
            publish.WithDockerfileBuilder(workingDirectory, context =>
            {
                var buildArgs = new List<string> { "build", "-o", "server" };

                if (buildTags is { Length: > 0 })
                {
                    buildArgs.Add("-tags");
                    buildArgs.Add(string.Join(",", buildTags));
                }

                buildArgs.Add(executable);

                // Get custom base image from annotation, if present
                context.Resource.TryGetLastAnnotation<DockerfileBaseImageAnnotation>(out var baseImageAnnotation);
                var goVersion = baseImageAnnotation?.BuildImage ?? GetDefaultGoBaseImage(workingDirectory, context.Services);

                var buildStage = context.Builder
                    .From(goVersion, "builder")
                    .WorkDir("/build")
                    .Copy(".", "./")
                    .Run(string.Join(" ", ["CGO_ENABLED=0", "go", .. buildArgs]));

                var runtimeImage = baseImageAnnotation?.RuntimeImage ?? $"alpine:{DefaultAlpineVersion}";

                context.Builder
                    .From(runtimeImage)
                    .Run("apk --no-cache add ca-certificates")
                    .WorkDir("/app")
                    .CopyFrom(buildStage.StageName!, "/build/server", "/app/server")
                    .Entrypoint(["/app/server"]);
            });
        });
    }
#pragma warning restore ASPIREDOCKERFILEBUILDER001

    private static string GetDefaultGoBaseImage(string workingDirectory, IServiceProvider serviceProvider)
    {
        const string DefaultGoVersion = "1.23";
        var logger = serviceProvider.GetService<ILogger<GolangAppExecutableResource>>() ?? NullLogger<GolangAppExecutableResource>.Instance;
        var goVersion = DetectGoVersion(workingDirectory, logger) ?? DefaultGoVersion;
        return $"golang:{goVersion}";
    }

    /// <summary>
    /// Detects the Go version to use for a project by checking go.mod and the installed Go toolchain.
    /// </summary>
    /// <param name="workingDirectory">The working directory of the Go project.</param>
    /// <param name="logger">The logger for diagnostic messages.</param>
    /// <returns>The detected Go version as a string, or <c>null</c> if no version is detected.</returns>
    internal static string? DetectGoVersion(string workingDirectory, ILogger logger)
    {
        // Check go.mod file
        var goModPath = Path.Combine(workingDirectory, "go.mod");
        if (File.Exists(goModPath))
        {
            try
            {
                var goModContent = File.ReadAllText(goModPath);
                // Look for "go X.Y" or "go X.Y.Z" line in go.mod
                var match = Regex.Match(goModContent, @"^\s*go\s+(\d+\.\d+(?:\.\d+)?)", RegexOptions.Multiline);
                if (match.Success)
                {
                    var version = match.Groups[1].Value;
                    // Extract major.minor (e.g., "1.22" from "1.22.3")
                    var versionParts = version.Split('.');
                    if (versionParts.Length >= 2)
                    {
                        var majorMinor = $"{versionParts[0]}.{versionParts[1]}";
                        logger.LogDebug("Detected Go version {Version} from go.mod file", majorMinor);
                        return majorMinor;
                    }
                }
            }
            catch (IOException ex)
            {
                logger.LogDebug(ex, "Failed to parse go.mod file due to IO error");
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogDebug(ex, "Failed to parse go.mod file due to unauthorized access");
            }
            catch (RegexMatchTimeoutException ex)
            {
                logger.LogDebug(ex, "Failed to parse go.mod file due to regex timeout");
            }
        }

        // Try to detect from installed Go toolchain
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "go",
                Arguments = "version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                // Read both output and error asynchronously to avoid deadlock
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                var output = outputTask.GetAwaiter().GetResult();

                if (process.ExitCode == 0)
                {
                    // Output format: "go version goX.Y.Z ..."
                    var match = Regex.Match(output, @"go version go(\d+\.\d+)");
                    if (match.Success)
                    {
                        var version = match.Groups[1].Value;
                        logger.LogDebug("Detected Go version {Version} from installed toolchain", version);
                        return version;
                    }
                }
            }
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Failed to detect Go version from installed toolchain due to IO error");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            logger.LogDebug(ex, "Failed to detect Go version from installed toolchain - go command not found or not executable");
        }

        logger.LogDebug("No Go version detected, will use default version");
        return null;
    }
}
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Dockerfile generation methods for Perl application resources.
/// </summary>
public static partial class PerlAppResourceBuilderExtensions
{
    /// <summary>
    /// Builds the command arguments used as a container entrypoint based on
    /// the configured Perl entrypoint type.
    /// </summary>
    /// <param name="entrypointType">The entrypoint invocation mode.</param>
    /// <param name="entrypoint">The script/module/executable entrypoint.</param>
    /// <param name="apiSubcommand">Optional API subcommand, defaults to <c>daemon</c> for API mode.</param>
    /// <param name="useLocalLibPath">Whether to include local::lib include-path arguments.</param>
    /// <returns>The argument vector for Docker <c>ENTRYPOINT</c>.</returns>
    [Experimental("CTASPIREPERL002")]
    internal static string[] BuildContainerEntrypointArguments(
        EntrypointType entrypointType,
        string entrypoint,
        string? apiSubcommand,
        bool useLocalLibPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);

        if (entrypointType == EntrypointType.Executable)
        {
            return [entrypoint];
        }

        List<string> args = ["perl"];
        if (useLocalLibPath)
        {
            args.Add("-Ilocal/lib/perl5");
        }

        switch (entrypointType)
        {
            case EntrypointType.Module:
                args.Add($"-M{entrypoint}");
                args.Add("-e");
                args.Add($"{entrypoint}->run()");
                break;

            case EntrypointType.API:
                args.Add(entrypoint);
                args.Add(string.IsNullOrWhiteSpace(apiSubcommand) ? "daemon" : apiSubcommand);
                break;

            default:
                args.Add(entrypoint);
                break;
        }

        return [.. args];
    }

    /// <summary>
    /// Configures the Perl resource for publish (deployment) mode by adding
    /// <c>PublishAsDockerFile</c> and a Dockerfile builder callback that generates
    /// a Dockerfile tailored to the active package manager.
    /// </summary>
    /// <param name="resourceBuilder">The Perl resource builder being configured for publish mode.</param>
    /// <param name="entrypointType">The application entrypoint type.</param>
    /// <param name="entrypoint">The script/module/executable entrypoint value.</param>
    /// <param name="apiSubcommand">Optional API subcommand used for API entrypoints.</param>
    private static void ConfigurePublishMode(
        IResourceBuilder<PerlAppResource> resourceBuilder,
        EntrypointType entrypointType,
        string entrypoint,
        string? apiSubcommand)
    {
        resourceBuilder.PublishAsDockerFile();

#pragma warning disable ASPIREDOCKERFILEBUILDER001
        resourceBuilder.WithAnnotation(new DockerfileBuilderCallbackAnnotation(context =>
        {
            var resource = context.Resource;

            // Determine package manager strategy
            var packageManager = PerlPackageManager.Cpanm; // default for publish
            if (resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var pmAnnotation))
            {
                packageManager = pmAnnotation.PackageManager;
            }

            // Determine base image — allow user override via DockerfileBaseImageAnnotation
            var baseImage = "perl:5-slim";
            var buildImage = "perl:5";
            if (resource.TryGetLastAnnotation<DockerfileBaseImageAnnotation>(out var baseImageAnnotation))
#pragma warning restore ASPIREDOCKERFILEBUILDER001
            {
                if (baseImageAnnotation.RuntimeImage is { } runtimeImage)
                {
                    baseImage = runtimeImage;
                }

                if (baseImageAnnotation.BuildImage is { } bi)
                {
                    buildImage = bi;
                }
            }

            // Resolve local::lib path from annotation (if configured)
            string? localLibPath = null;
            if (resource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out var localLibAnnotation))
            {
                localLibPath = localLibAnnotation.Path;
            }

            // Resolve carton deployment flag from annotation (default: true for publish)
            var cartonDeployment = true;
            if (resource.TryGetLastAnnotation<PerlCartonDeploymentAnnotation>(out var cartonDeploymentAnnotation))
            {
                cartonDeployment = cartonDeploymentAnnotation.UseDeployment;
            }

            switch (packageManager)
            {
                case PerlPackageManager.Carton:
#pragma warning disable CTASPIREPERL002 // Dockerfile generation is experimental
                    BuildCartonDockerfile(context.Builder, entrypointType, entrypoint, apiSubcommand, baseImage, buildImage, localLibPath, cartonDeployment);
#pragma warning restore CTASPIREPERL002
                    break;

                default:
#pragma warning disable CTASPIREPERL002 // Dockerfile generation is experimental
                    BuildCpanmDockerfile(context.Builder, entrypointType, entrypoint, apiSubcommand, baseImage, localLibPath);
#pragma warning restore CTASPIREPERL002
                    break;
            }

            return Task.CompletedTask;
        }));
    }

    /// <summary>
    /// Builds a single-stage Dockerfile using cpanm for dependency installation.
    /// </summary>
    /// <param name="builder">The Dockerfile builder to append instructions to.</param>
    /// <param name="entrypointType">The application entrypoint type.</param>
    /// <param name="entrypoint">The script/module/executable entrypoint value.</param>
    /// <param name="apiSubcommand">Optional API subcommand used for API entrypoints.</param>
    /// <param name="baseImage">The base runtime image used for the generated Dockerfile.</param>
    /// <param name="localLibPath">Optional local::lib path to install and resolve modules from.</param>
    [Experimental("CTASPIREPERL002")]
    internal static void BuildCpanmDockerfile(
        this DockerfileBuilder builder,
        EntrypointType entrypointType,
        string entrypoint,
        string? apiSubcommand,
        string baseImage,
        string? localLibPath = null)
    {
        var useLocalLib = localLibPath is not null;
        var entrypointArgs = BuildContainerEntrypointArguments(entrypointType, entrypoint, apiSubcommand, useLocalLibPath: useLocalLib);

        var stage = builder.From(baseImage)
            .WorkDir("/app")
            .Run("cpanm App::cpanminus || true")
            .Copy("cpanfile", "./")
            .Run(useLocalLib
                ? $"cpanm --local-lib /app/{localLibPath} --installdeps --notest ."
                : "cpanm --installdeps --notest .")
            .Copy(".", ".");

        if (useLocalLib)
        {
            var libPerl5 = $"/app/{localLibPath}/lib/perl5";
            stage.Env("PERL5LIB", libPerl5)
                .Env("PERL_LOCAL_LIB_ROOT", $"/app/{localLibPath}");
        }

        stage.Entrypoint(entrypointArgs);
    }

    /// <summary>
    /// Builds a multi-stage Dockerfile using Carton for reproducible dependency resolution.
    /// </summary>
    /// <param name="builder">The Dockerfile builder to append instructions to.</param>
    /// <param name="entrypointType">The application entrypoint type.</param>
    /// <param name="entrypoint">The script/module/executable entrypoint value.</param>
    /// <param name="apiSubcommand">Optional API subcommand used for API entrypoints.</param>
    /// <param name="runtimeImage">The runtime-stage image.</param>
    /// <param name="buildImage">The build-stage image.</param>
    /// <param name="localLibPath">Optional local::lib path to expose in the runtime stage.</param>
    /// <param name="cartonDeployment">Whether to use <c>carton install --deployment</c>.</param>
    [Experimental("CTASPIREPERL002")]
    internal static void BuildCartonDockerfile(
        this DockerfileBuilder builder,
        EntrypointType entrypointType,
        string entrypoint,
        string? apiSubcommand,
        string runtimeImage,
        string buildImage,
        string? localLibPath = null,
        bool cartonDeployment = true)
    {
        var useLocalLib = localLibPath is not null;
        var entrypointArgs = BuildContainerEntrypointArguments(entrypointType, entrypoint, apiSubcommand, useLocalLibPath: true);

        var cartonInstallCommand = cartonDeployment
            ? "carton install --deployment"
            : "carton install";

        builder.From(buildImage, "build")
            .WorkDir("/app")
            .Run("cpanm Carton")
            .Copy("cpanfile", "./")
            .Copy("cpanfile.snapshot", "./")
            .Run(cartonInstallCommand)
            .Copy(".", ".");

        var runtimeStage = builder.From(runtimeImage)
            .WorkDir("/app")
            .CopyFrom("build", "/app", "/app");

        if (useLocalLib)
        {
            var libPerl5 = $"/app/{localLibPath}/lib/perl5";
            runtimeStage.Env("PERL5LIB", libPerl5)
                .Env("PERL_LOCAL_LIB_ROOT", $"/app/{localLibPath}");
        }

        runtimeStage.Entrypoint(entrypointArgs);
    }
}

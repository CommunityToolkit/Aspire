using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Hosting.Perl.Services;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Perl application resources to the application model.
/// </summary>
public static partial class PerlAppResourceBuilderExtensions
{
    private const string DefaultPerlEnvironment = "perl";
    private const PerlPackageManager DefaultPackageManager = PerlPackageManager.Cpan;

    /// <summary>
    /// Adds a Perl script resource (worker, CLI tool, background service) to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="appDirectory">
    /// The path to the directory containing the Perl script. Resolved relative to the AppHost
    /// project directory; becomes the resource's working directory and the anchor for all relative
    /// path resolution (script path, <c>WithLocalLib</c>, cpanfile discovery).
    /// </param>
    /// <param name="scriptName">
    /// The path to the script relative to <paramref name="appDirectory"/>.
    /// Do not include the <paramref name="appDirectory"/> prefix — the script is resolved
    /// relative to <paramref name="appDirectory"/> automatically.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a Perl script directly using <c>perl -s scriptName</c>.
    /// The working directory is set to <paramref name="appDirectory"/> and all relative paths
    /// (including <c>WithLocalLib</c> and cpanfile discovery) resolve against it.
    /// </para>
    /// </remarks>
    /// <example>
    /// For your AppHost / Application Model, you'd add a Perl script like this:
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddPerlScript("my-worker", "../scripts", "Worker.pl");
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<PerlAppResource> AddPerlScript(
        this IDistributedApplicationBuilder builder, [ResourceName] string resourceName, string appDirectory, string scriptName)
        => AddPerlAppCore(builder, resourceName, appDirectory, EntrypointType.Script, scriptName, DefaultPerlEnvironment);

    /// <summary>
    /// Adds a Perl API server resource (e.g., Mojolicious, Dancer2) to the application model.
    /// Passes the <c>daemon</c> subcommand so HTTP frameworks start a listener.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="appDirectory">
    /// The path to the directory containing the Perl script. Resolved relative to the AppHost
    /// project directory; becomes the resource's working directory and the anchor for all relative
    /// path resolution (script path, <c>WithLocalLib</c>, cpanfile discovery).
    /// </param>
    /// <param name="scriptName">
    /// The API script path, relative to <paramref name="appDirectory"/>.
    /// Do not include the <paramref name="appDirectory"/> prefix — the script is resolved
    /// relative to <paramref name="appDirectory"/> automatically.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method configures a Perl API script and passes the default API subcommand
    /// (<c>daemon</c>) so frameworks such as Mojolicious start an HTTP server.
    /// The working directory is set to <paramref name="appDirectory"/> and all relative paths
    /// (including <c>WithLocalLib</c> and cpanfile discovery) resolve against it.
    /// </para>
    /// </remarks>
    /// <example>
    /// For your AppHost / Application Model, you'd add a Perl API like this:
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddPerlApi("perl-api", "../api", "API.pl");
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<PerlAppResource> AddPerlApi(
        this IDistributedApplicationBuilder builder, [ResourceName] string resourceName, string appDirectory, string scriptName)
        => AddPerlAppCore(builder, resourceName, appDirectory, EntrypointType.API, scriptName, DefaultPerlEnvironment, "daemon");

    /// <summary>
    /// Adds a Perl module to the application model. The module is executed using
    /// <c>perl -MModule::Name -e "Module::Name-&gt;run()"</c>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="appDirectory">The path to the working directory for the application.</param>
    /// <param name="moduleName">The fully qualified Perl module name (e.g., <c>"MyApp::Main"</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <example>
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    /// builder.AddPerlModule("worker", "../perl-worker", "MyApp::Worker");
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<PerlAppResource> AddPerlModule(
        this IDistributedApplicationBuilder builder, [ResourceName] string resourceName, string appDirectory, string moduleName)
        => AddPerlAppCore(builder, resourceName, appDirectory, EntrypointType.Module, moduleName, DefaultPerlEnvironment);

    /// <summary>
    /// Adds a Perl executable (compiled binary or PAR-packed application) to the application model.
    /// The executable is run directly rather than through the <c>perl</c> interpreter.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="appDirectory">The path to the working directory for the application.</param>
    /// <param name="executablePath">The path to the executable, relative to <paramref name="appDirectory"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <example>
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    /// builder.AddPerlExecutable("myapp", "../perl-app", "my-compiled-perl");
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<PerlAppResource> AddPerlExecutable(
        this IDistributedApplicationBuilder builder, [ResourceName] string resourceName, string appDirectory, string executablePath)
        => AddPerlAppCore(builder, resourceName, appDirectory, EntrypointType.Executable, executablePath, DefaultPerlEnvironment);


    /// <summary>
    /// Core implementation used by all <c>AddPerl*</c> entrypoint helpers.
    /// Resolves the working directory, wires command/arguments, configures
    /// required command checks, telemetry environment variables, and mode-specific
    /// startup/publish behavior.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="resourceName">The resource name to register.</param>
    /// <param name="appDirectory">The application working directory path.</param>
    /// <param name="entrypointType">How the entrypoint should be invoked.</param>
    /// <param name="entrypoint">The script/module/executable entrypoint.</param>
    /// <param name="virtualEnvironmentPath">Reserved environment path argument for future compatibility.</param>
    /// <param name="typeOfApi">Optional API subcommand such as <c>daemon</c>.</param>
    /// <returns>The configured Perl application resource builder.</returns>
    private static IResourceBuilder<PerlAppResource> AddPerlAppCore(
        IDistributedApplicationBuilder builder, string resourceName, string appDirectory, EntrypointType entrypointType,
        string entrypoint, string virtualEnvironmentPath, string? typeOfApi = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentNullException.ThrowIfNull(appDirectory);
        ArgumentException.ThrowIfNullOrEmpty(entrypoint);
        ArgumentNullException.ThrowIfNull(virtualEnvironmentPath);

        // Resolve the app directory against the AppHost directory so behavior is
        // consistent between aspire run and integration test fixtures.
        var resolvedAppDirectory = Path.IsPathRooted(appDirectory)
            ? appDirectory
            : Path.GetFullPath(appDirectory, builder.AppHostDirectory);

        var resource = new PerlAppResource(resourceName, entrypoint, resolvedAppDirectory);

        if (builder.ExecutionContext.IsRunMode)
        {
            builder.Eventing.Subscribe<BeforeStartEvent>((evt, _) =>
            {
                var logger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
                logger.LogInformation("Perl resource '{ResourceName}' configured: entrypoint={Entrypoint}, type={EntrypointType}, workingDirectory={WorkingDirectory}",
                    resourceName, entrypoint, entrypointType, resolvedAppDirectory);
                return Task.CompletedTask;
            });
        }

        // Use just the entrypoint filename — the working directory is already set to appDirectory,
        // so Path.Combine(appDirectory, entrypoint) would double-nest the path.
        string scriptPath = entrypoint;

        // Determine command and args based on entrypoint type
        var command = entrypointType == EntrypointType.Executable ? entrypoint : "perl";

        var resourceBuilder = builder
            .AddResource(resource)
            .WithAnnotation(new PerlEntrypointAnnotation
            {
                Type = entrypointType,
                Entrypoint = entrypoint
            })
            .WithAnnotation(new PerlPackageManagerAnnotation(DefaultPackageManager))
            .WithCommand(command);

        // Configure args per entrypoint type
        // Note: -s enables Perl's switch processing and is appropriate for plain scripts.
        // API/Mojolicious scripts use app->start which reads @ARGV directly, so the
        // subcommand (e.g. "daemon") must be a separate positional argument without -s.
        switch (entrypointType)
        {
            case EntrypointType.Module:
                resourceBuilder.WithArgs($"-M{entrypoint}", "-e", $"{entrypoint}->run()");
                break;
            case EntrypointType.Executable:
                // Executable runs directly — no perl interpreter args needed
                break;
            case EntrypointType.API:
                if (typeOfApi is not null)
                {
                    resourceBuilder.WithArgs(scriptPath, typeOfApi);
                }
                else
                {
                    resourceBuilder.WithArgs(scriptPath);
                }
                break;
            default:
                resourceBuilder.WithArgs("-s", scriptPath);
                break;
        }

        resourceBuilder.WithOtlpExporter();

        resourceBuilder.WithRequiredCommand("perl", "https://www.perl.org/get.html");
        resourceBuilder.WithRequiredCommand("cpan", "https://metacpan.org/pod/CPAN");

        // Configure OpenTelemetry exporters using environment variables
        // https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/#exporter-selection
        // The Perl OpenTelemetry SDK defaults to JSON encoding when Google::ProtocolBuffers::Dynamic
        // is not available. Using http/json avoids a silent protocol mismatch where the SDK sends
        // JSON-encoded data with a protobuf content-type that the OTLP collector rejects.
        resourceBuilder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["OTEL_TRACES_EXPORTER"] = "otlp";
            context.EnvironmentVariables["OTEL_LOGS_EXPORTER"] = "otlp";
            context.EnvironmentVariables["OTEL_METRICS_EXPORTER"] = "otlp";
            context.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";

            context.EnvironmentVariables["OTEL_PERL_TRACES_EXPORTER"] = "otlp";
            context.EnvironmentVariables["OTEL_PERL_LOGS_EXPORTER"] = "otlp";
            context.EnvironmentVariables["OTEL_PERL_METRICS_EXPORTER"] = "otlp";
            context.EnvironmentVariables["OTEL_PERL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";
        });

        if (builder.ExecutionContext.IsRunMode)
        {
            // Auto-detect project-level dependency files (GAP-11)
            //FEEDBACK: I don't know that I like this behavior.  Please explain why I should do this during runtime, 
            //is this because it happens automatically on publish?
            if (HasDependencyFiles(resolvedAppDirectory, builder.AppHostDirectory))
            {
                resourceBuilder.WithProjectDependencies();
            }

            /* 
             * Subscribe to BeforeStartEvent for this specific resource to wire up dependencies dynamically
             * This allows methods like WithPip, WithUv, and WithVirtualEnvironment to add/remove resources 
             * and the dependencies will be established based on which resources actually exist 
             * Only do this in run mode since the installer and venv creator only run in run mode        
             * https://github.com/dotnet/aspire/pull/12663/commits/c60eb2189bde0ae9b3b0dd03ec37a591103c7b63
             * */
            var resourceToSetup = resourceBuilder.Resource;
            builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
            {
                SetupDependencies(builder, resourceToSetup);
                return Task.CompletedTask;
            });
        }

        if (builder.ExecutionContext.IsPublishMode)
        {
            ConfigurePublishMode(resourceBuilder, entrypointType, entrypoint, typeOfApi);
        }

        return resourceBuilder;
    }

    /// <summary>
    /// Establishes dependency ordering between installer child resources.
    /// When both a project-level installer and per-package installers exist,
    /// per-package installers are ordered to wait for the project installer,
    /// producing the DAG: project-deps-installer → per-package-installers → app.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="resource">The Perl resource whose installer dependencies are updated.</param>
    internal static void SetupDependencies(IDistributedApplicationBuilder builder, PerlAppResource resource)
    {
        var hasProjectInstaller = resource.TryGetLastAnnotation<PerlProjectInstallerAnnotation>(out var projectAnnotation);

        if (!hasProjectInstaller)
        {
            return;
        }

        foreach (var moduleAnnotation in resource.Annotations.OfType<PerlModuleInstallerAnnotation>())
        {
            // Make each per-package installer wait for the project-level installer to finish first
            if (hasProjectInstaller)
            {
                AddWaitIfMissing(moduleAnnotation.Resource, projectAnnotation!.Resource);
            }
        }
    }

    /// <summary>
    /// Adds a completion wait edge from <paramref name="targetResource"/> to
    /// <paramref name="dependencyResource"/> when one does not already exist.
    /// </summary>
    /// <param name="targetResource">The resource that should wait.</param>
    /// <param name="dependencyResource">The resource that must complete first.</param>
    private static void AddWaitIfMissing(IResource targetResource, IResource dependencyResource)
    {
        var hasExisting = targetResource.Annotations
            .OfType<WaitAnnotation>()
            .Any(wait => wait.Resource == dependencyResource && wait.WaitType == WaitType.WaitForCompletion);

        if (hasExisting)
        {
            return;
        }

        targetResource.Annotations.Add(new WaitAnnotation(dependencyResource, WaitType.WaitForCompletion, 0));
    }

    /// <summary>
    /// Configures the Perl application to use a specific perlbrew-managed Perl version.
    /// </summary>
    /// <typeparam name="T">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="version">
    /// The perlbrew version name. Accepts both <c>"5.38.0"</c> and <c>"perl-5.38.0"</c>.
    /// </param>
    /// <param name="perlbrewRoot">
    /// Optional explicit path to the perlbrew root directory. If <c>null</c>, resolves from
    /// the <c>PERLBREW_ROOT</c> environment variable, or defaults to <c>~/perl5/perlbrew</c>.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithPerlbrew<T>(
        this IResourceBuilder<T> builder, string version, string? perlbrewRoot = null) where T : PerlAppResource
        => builder.WithPerlbrewEnvironment(version, perlbrewRoot);

    /// <summary>
    /// Configures the Perl application to use a specific perlbrew-managed Perl version.
    /// This resolves the Perl executable from the perlbrew installation and updates the resource's
    /// command and environment variables so that all subsequent operations use the specified Perl version.
    /// </summary>
    /// <typeparam name="T">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="version">
    /// The perlbrew version name. Accepts both <c>"5.38.0"</c> and <c>"perl-5.38.0"</c>.
    /// </param>
    /// <param name="perlbrewRoot">
    /// Optional explicit path to the perlbrew root directory. If <c>null</c>, resolves from
    /// the <c>PERLBREW_ROOT</c> environment variable, or defaults to <c>~/perl5/perlbrew</c>.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// Perlbrew is Linux-only. On Windows, an <see cref="InvalidOperationException"/> is thrown
    /// with a recommendation to use <see href="https://github.com/stevieb9/berrybrew">Berrybrew</see>.
    /// </para>
    /// <para>
    /// When combined with <see cref="WithLocalLib{TResource}"/>, modules are installed into the
    /// local directory rather than the perlbrew tree, keeping the perlbrew installation clean
    /// and enabling per-project module isolation.
    /// </para>
    /// </remarks>
    public static IResourceBuilder<T> WithPerlbrewEnvironment<T>(
        this IResourceBuilder<T> builder, string version, string? perlbrewRoot = null) where T : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(version);

        // Perlbrew is unsupported on Windows. Fail early with explicit guidance and avoid
        // any perlbrew path or version checks in this code path.
        if (OperatingSystem.IsWindows())
        {
            const string windowsMessage =
                "Perlbrew is unsupported on Windows. " +
                "The recommendation is to use Berrybrew. " +
                "Support for Berrybrew is on the roadmap for a future release.";
            const string berrybrewLink = "https://github.com/stevieb9/berrybrew";

            builder.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(builder.Resource, async (evt, ct) =>
            {
#pragma warning disable ASPIREINTERACTION001
                var interactionService = evt.Services.GetService<IInteractionService>();
                if (interactionService is not null && interactionService.IsAvailable)
                {
                    await interactionService.PromptNotificationAsync(
                        title: "Perlbrew on Windows",
                        message: windowsMessage,
                        options: new NotificationInteractionOptions
                        {
                            Intent = MessageIntent.Warning,
                            LinkText = "Installation instructions",
                            LinkUrl = berrybrewLink,
                        },
                        cancellationToken: ct).ConfigureAwait(false);
                }
#pragma warning restore ASPIREINTERACTION001

                throw new InvalidOperationException(windowsMessage);
            });

            return builder;
        }

        var normalizedVersion = PerlbrewEnvironment.NormalizeVersion(
            PerlVersionDetector.NormalizeVersionString(version));
        var resolvedRoot = PerlbrewEnvironment.ResolvePerlbrewRoot(perlbrewRoot);
        var environment = new PerlbrewEnvironment(resolvedRoot, normalizedVersion);

        // Resolve the perl executable from the perlbrew environment
        var perlExecutable = environment.GetExecutable("perl");

        // Switch the resource's command to use the perlbrew perl
        builder.WithCommand(perlExecutable);

        // Attach the annotation with the resolved environment
        builder.WithAnnotation(new PerlbrewEnvironmentAnnotation(normalizedVersion, "perlbrew")
        {
            Environment = environment
        }, ResourceAnnotationMutationBehavior.Replace);

        // Set perlbrew environment variables so child processes and libraries resolve correctly
        EnsurePerlbrewEnvironmentCallback(builder);

        // Validate that the perlbrew perl version is installed.
#pragma warning disable ASPIRECOMMAND001
        builder.WithRequiredCommand("perlbrew", _ => Task.FromResult(
            File.Exists(perlExecutable)
                ? RequiredCommandValidationResult.Success()
                : RequiredCommandValidationResult.Failure(
                    $"Perlbrew Perl version '{normalizedVersion}' is not installed. " +
                    $"Expected: '{perlExecutable}'. " +
                    $"Install with: perlbrew install {normalizedVersion}. " +
                    "You may install perlbrew on linux with 'sudo apt install perlbrew' or using the intructions at the perlbrew website.")),
            "https://perlbrew.pl/");
#pragma warning restore ASPIRECOMMAND001

        return builder;
    }

    /// <summary>
    /// Configures the Perl application to use a local::lib directory for module isolation.
    /// Sets <c>PERL5LIB</c>, <c>PERL_LOCAL_LIB_ROOT</c>, <c>PERL_MM_OPT</c>, and <c>PERL_MB_OPT</c>
    /// environment variables so that modules are resolved from and installed into the local directory.
    /// </summary>
    /// <typeparam name="TResource">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="path">
    /// The path to the local::lib directory. Relative paths are resolved against
    /// the resource's working directory (<c>appDirectory</c>), not the AppHost project.
    /// Rooted paths (e.g., <c>/opt/perl-libs</c> or <c>C:\perl-libs</c>) are used as-is.
    /// Defaults to <c>"local"</c> (the Carton convention).
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    /// <remarks>
    /// Sets the following environment variables:
    /// <list type="bullet">
    /// <item><c>PERL5LIB</c> — <c>&lt;resolved&gt;/lib/perl5</c> (module search path)</item>
    /// <item><c>PERL_LOCAL_LIB_ROOT</c> — <c>&lt;resolved&gt;</c></item>
    /// <item><c>PERL_MM_OPT</c> — <c>INSTALL_BASE=&lt;resolved&gt;</c></item>
    /// <item><c>PERL_MB_OPT</c> — <c>--install_base &lt;resolved&gt;</c></item>
    /// </list>
    /// These ensure modules are resolved from and installed into the local directory
    /// without requiring system-level permissions.
    /// </remarks>
    public static IResourceBuilder<TResource> WithLocalLib<TResource>(
        this IResourceBuilder<TResource> builder,
        string path = "local") where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(path);

        builder.WithAnnotation(new PerlLocalLibAnnotation(path),
            ResourceAnnotationMutationBehavior.Replace);

        EnsureLocalLibEnvironmentCallback(builder);

        TryAttachLocalLibEnvironmentToProjectInstaller(builder);
        TryAttachLocalLibToExistingInstallers(builder);

        return builder;
    }

    /// <summary>
    /// Configures certificate trust for the Perl application by setting SSL/TLS environment
    /// variables that common Perl HTTP libraries respect.
    /// Sets <c>SSL_CERT_FILE</c> (IO::Socket::SSL / LWP), <c>PERL_LWP_SSL_CA_FILE</c> (LWP::UserAgent),
    /// and <c>MOJO_CA_FILE</c> (Mojolicious) to the certificate bundle path provided by Aspire.
    /// </summary>
    /// <typeparam name="TResource">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    [Experimental("CTASPIREPERL001")]
    public static IResourceBuilder<TResource> WithPerlCertificateTrust<TResource>(
        this IResourceBuilder<TResource> builder) where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.Annotations.OfType<PerlCertificateTrustAnnotation>().Any())
        {
            return builder;
        }

        builder.Resource.Annotations.Add(new PerlCertificateTrustAnnotation());

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            builder.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((evt, _) =>
            {
                var logger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(builder.Resource);
                var sslCertFile = Environment.GetEnvironmentVariable("SSL_CERT_FILE");
                if (sslCertFile is not null)
                {
                    logger.LogInformation("Certificate trust configured: SSL_CERT_FILE={CertBundlePath}", sslCertFile);
                }
                else
                {
                    logger.LogDebug("Certificate trust enabled but no SSL_CERT_FILE found in environment");
                }

                return Task.CompletedTask;
            });
        }

        builder.WithCertificateTrustConfiguration(context =>
        {
            if (context.CertificateBundlePath is { } bundlePath)
            {
                context.EnvironmentVariables["SSL_CERT_FILE"] = bundlePath;
                context.EnvironmentVariables["PERL_LWP_SSL_CA_FILE"] = bundlePath;
                context.EnvironmentVariables["MOJO_CA_FILE"] = bundlePath;
            }

            return Task.CompletedTask;
        });

        // Propagate cert trust env vars to any existing installer child resources
        TryAttachCertificateTrustToExistingInstallers(builder);

        return builder;
    }

    /// <summary>
    /// Resolves the local::lib path from the resource's <see cref="PerlLocalLibAnnotation"/>,
    /// returning <c>null</c> when no local::lib is configured.
    /// </summary>
    /// <param name="resource">The Perl resource to inspect for local::lib annotations.</param>
    /// <returns>The resolved local::lib path, or <c>null</c> when not configured.</returns>
    private static string? ResolveLocalLibPath(PerlAppResource resource)
    {
        if (!resource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out var localLibAnnotation))
        {
            return null;
        }

        return ResolveLocalLibPath(resource.WorkingDirectory, localLibAnnotation.Path);
    }

    /// <summary>
    /// Resolves a configured local::lib path against the resource working directory.
    /// </summary>
    /// <param name="workingDirectory">The base working directory.</param>
    /// <param name="configuredPath">The configured local::lib path value.</param>
    /// <returns>The absolute local::lib path.</returns>
    private static string ResolveLocalLibPath(string? workingDirectory, string configuredPath)
    {
        var appDir = workingDirectory ?? ".";
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(appDir, configuredPath));
    }

    /// <summary>
    /// Applies perlbrew environment variables to an environment-variable dictionary.
    /// </summary>
    /// <param name="environmentVariables">The destination environment variable map.</param>
    /// <param name="perlbrewEnvironment">The perlbrew environment metadata.</param>
    private static void ApplyPerlbrewEnvironment(
        IDictionary<string, object> environmentVariables,
        PerlbrewEnvironment perlbrewEnvironment)
    {
        environmentVariables["PERLBREW_ROOT"] = perlbrewEnvironment.PerlbrewRoot;
        environmentVariables["PERLBREW_PERL"] = perlbrewEnvironment.Version;
        environmentVariables["PERLBREW_HOME"] = Path.Combine(perlbrewEnvironment.PerlbrewRoot, ".perlbrew");

        var binPath = perlbrewEnvironment.BinPath;
        var existingPath = environmentVariables.ContainsKey("PATH")
            ? environmentVariables["PATH"]
            : null;

        environmentVariables["PATH"] = existingPath is not null
            ? $"{binPath}{Path.PathSeparator}{existingPath}"
            : binPath;
    }

    /// <summary>
    /// Applies local::lib environment variables to an environment-variable dictionary.
    /// </summary>
    /// <param name="environmentVariables">The destination environment variable map.</param>
    /// <param name="localLibPath">The resolved local::lib directory path.</param>
    private static void ApplyLocalLibEnvironment(IDictionary<string, object> environmentVariables, string localLibPath)
    {
        var libPerl5 = Path.Combine(localLibPath, "lib", "perl5");

        environmentVariables["PERL5LIB"] = libPerl5;
        environmentVariables["PERL_LOCAL_LIB_ROOT"] = localLibPath;
        environmentVariables["PERL_MM_OPT"] = $"INSTALL_BASE={localLibPath}";
        environmentVariables["PERL_MB_OPT"] = $"--install_base {localLibPath}";
    }

    /// <summary>
    /// Ensures the resource has a single callback that applies perlbrew
    /// environment variables at runtime.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="resource">The resource builder to configure.</param>
    private static void EnsurePerlbrewEnvironmentCallback<TResource>(
        IResourceBuilder<TResource> resource) where TResource : PerlAppResource
    {
        if (resource.Resource.Annotations.OfType<PerlbrewResourceEnvironmentAnnotation>().Any())
        {
            return;
        }

        resource.Resource.Annotations.Add(new PerlbrewResourceEnvironmentAnnotation());

        if (resource.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            resource.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((evt, _) =>
            {
                if (resource.Resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var annotation) &&
                    annotation.Environment is { } env)
                {
                    var logger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource.Resource);
                    logger.LogInformation("Perlbrew environment: version={Version}, root={PerlbrewRoot}, perl={PerlPath}",
                        env.Version, env.PerlbrewRoot, env.GetExecutable("perl"));
                }

                return Task.CompletedTask;
            });
        }

        resource.WithEnvironment(context =>
        {
            if (resource.Resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var perlbrewAnnotation) &&
                perlbrewAnnotation.Environment is { } perlbrewEnvironment)
            {
                ApplyPerlbrewEnvironment(context.EnvironmentVariables, perlbrewEnvironment);
            }
        });
    }

    /// <summary>
    /// Ensures the resource has a single callback that applies local::lib
    /// environment variables at runtime.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="resource">The resource builder to configure.</param>
    private static void EnsureLocalLibEnvironmentCallback<TResource>(
        IResourceBuilder<TResource> resource) where TResource : PerlAppResource
    {
        if (resource.Resource.Annotations.OfType<PerlLocalLibResourceEnvironmentAnnotation>().Any())
        {
            return;
        }

        resource.Resource.Annotations.Add(new PerlLocalLibResourceEnvironmentAnnotation());

        if (resource.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            resource.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((evt, _) =>
            {
                if (resource.Resource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out var annotation))
                {
                    var resolvedPath = ResolveLocalLibPath(resource.Resource.WorkingDirectory, annotation.Path);
                    var logger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource.Resource);
                    logger.LogInformation("local::lib configured: path='{ConfiguredPath}' resolved to '{ResolvedPath}'",
                        annotation.Path, resolvedPath);
                    logger.LogDebug("local::lib environment: PERL5LIB={Perl5Lib}, PERL_LOCAL_LIB_ROOT={LocalLibRoot}",
                        Path.Combine(resolvedPath, "lib", "perl5"), resolvedPath);
                }

                return Task.CompletedTask;
            });
        }

        resource.WithEnvironment(context =>
        {
            if (resource.Resource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out var localLibAnnotation))
            {
                var localLibPath = ResolveLocalLibPath(resource.Resource.WorkingDirectory, localLibAnnotation.Path);
                ApplyLocalLibEnvironment(context.EnvironmentVariables, localLibPath);
            }
        });
    }

    /// <summary>
    /// Checks whether the application directory contains standard Perl dependency files
    /// (<c>cpanfile</c>, <c>Makefile.PL</c>, or <c>Build.PL</c>).
    /// </summary>
    /// <param name="appDirectory">The application directory path, absolute or AppHost-relative.</param>
    /// <param name="appHostDirectory">The AppHost directory used to resolve relative paths.</param>
    /// <returns><c>true</c> when any supported dependency file is present; otherwise, <c>false</c>.</returns>
    internal static bool HasDependencyFiles(string appDirectory, string appHostDirectory)
    {
        var resolvedDir = Path.IsPathRooted(appDirectory)
            ? appDirectory
            : Path.GetFullPath(appDirectory, appHostDirectory);

        return File.Exists(Path.Combine(resolvedDir, "cpanfile"))
            || File.Exists(Path.Combine(resolvedDir, "Makefile.PL"))
            || File.Exists(Path.Combine(resolvedDir, "Build.PL"));
    }
}

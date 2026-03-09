using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using CommunityToolkit.Aspire.Hosting.Perl.Services;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable ASPIREEXTENSION001
#pragma warning disable ASPIRECOMMAND001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREDOCKERFILEBUILDER001

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding Perl application resources to the application model.
/// </summary>
public static class PerlAppResourceBuilderExtensions
{
    private const string DefaultPerlEnvironment = "perl";
    private const PerlPackageManager DefaultPackageManager = PerlPackageManager.Cpan;

    /// <summary>
    /// Adds a Perl script to the application model using a few default assumptions.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="appDirectory">The path to the directory containing the Perl script.</param>
    /// <param name="scriptName">The path to the script relative to the app directory to run.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a Perl script directly using the Command/Executable of <c>perl</c>.
    /// It assumes you intend to use <c>WithArgs</c> to pass arguments to the script, including the script 
    /// name itself.
    /// 
    /// For example: 
    /// <example>
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///     .WithCommand("perl")
    ///     .WithArgs("-s", $"{Path.GetFullPath(appDirectory, builder.AppHostDirectory)}/{scriptName}");
    /// </code>
    /// </example>
    /// </para>
    /// </remarks>
    /// <example>
    /// For your AppHost / Application Model, you'd add a Perl script like this:
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddPerlScript("fastapi-app", "../api", "main.pl");
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    public static IResourceBuilder<PerlAppResource> AddPerlScript(
        this IDistributedApplicationBuilder builder, [ResourceName] string resourceName, string appDirectory, string scriptName)
        => AddPerlAppCore(builder, resourceName, appDirectory, EntrypointType.Script, scriptName, DefaultPerlEnvironment);

    /// <summary>
    /// Adds a Perl script to the application model using a few default assumptions.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="appDirectory">The path to the directory containing the Perl script.</param>
    /// <param name="scriptName">The path to the script relative to the app directory to run.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a Perl script directly using the Command/Executable of <c>perl</c>.
    /// It assumes you intend to use <c>WithArgs</c> to pass arguments to the script, including the script 
    /// name itself.
    /// 
    /// For example: 
    /// <example>
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///     .WithCommand("perl")
    ///     .WithArgs("-s", $"{Path.GetFullPath(appDirectory, builder.AppHostDirectory)}/{scriptName}");
    /// </code>
    /// </example>
    /// </para>
    /// </remarks>
    /// <example>
    /// For your AppHost / Application Model, you'd add a Perl script like this:
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// builder.AddPerlScript("fastapi-app", "../api", "main.pl");
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

    /// <summary>42, and 7 
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

        builder.Services.TryAddSingleton<PerlInstallationManager>();

        var resource = new PerlAppResource(resourceName, entrypoint, resolvedAppDirectory);

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

        resourceBuilder.WithRequiredCommand("perl", async context =>
        {
            var manager = context.Services.GetRequiredService<PerlInstallationManager>();
            var isInstalled = await manager.IsPerlInstalledAsync(context.ResolvedPath, context.CancellationToken);

            return isInstalled
                ? RequiredCommandValidationResult.Success()
                : RequiredCommandValidationResult.Failure(
                    "Perl is not installed or not functional. " +
                    "Running 'perl -v' did not produce expected output starting with 'This is perl'.");
        }, "https://www.perl.org/get.html");

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

                throw new InvalidOperationException(windowsMessage);
            });

            return builder;
        }

        var normalizedVersion = PerlbrewEnvironment.NormalizeVersion(version);
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
        builder.WithEnvironment(context =>
        {
            ApplyPerlbrewEnvironment(context.EnvironmentVariables, environment);
        });

        // Validate that the perlbrew perl version is installed.
        builder.WithRequiredCommand("perlbrew", _ => Task.FromResult(
            File.Exists(perlExecutable)
                ? RequiredCommandValidationResult.Success()
                : RequiredCommandValidationResult.Failure(
                    $"Perlbrew Perl version '{normalizedVersion}' is not installed. " +
                    $"Expected: '{perlExecutable}'. " +
                    $"Install with: perlbrew install {normalizedVersion}. " +
                    "You may install perlbrew on linux with 'sudo apt install perlbrew' or using the intructions at the perlbrew website.")),
            "https://perlbrew.pl/");

        return builder;
    }

    /// <summary>
    /// Adds a CPANM module installation step to the Perl application resource prior to running the application.
    /// It will run a perl -M {moduleName} -e 1 command to check if the module is already installed,
    /// and if not, it will use CPANM to install the specified module, with --force if you supply it in installArgs.
    /// 
    /// Currently there is no interactive way to handle module installation prompts, so using --force is recommended.
    /// </summary>
    /// <typeparam name="TResource"></typeparam>
    /// <param name="resource"></param>
    /// <param name="moduleName"></param>
    /// <param name="installArgs"></param>
    /// <returns></returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use WithCpanMinus().WithPackage(moduleName) instead.")]
    public static IResourceBuilder<TResource> WithCpanm<TResource>(
        this IResourceBuilder<TResource> resource, string moduleName, string[]? installArgs = null) where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrEmpty(moduleName);

        installArgs ??= BuildInstallArgs(PerlPackageManager.Cpanm, moduleName, force: false, skipTest: false);

        resource
            .WithCpanMinus()
            .WithAnnotation(new PerlModuleInstallCommandAnnotation(installArgs), ResourceAnnotationMutationBehavior.Replace);

        AddInstaller(resource, moduleName, installArgs.Length > 0);

        return resource;
    }

    /// <summary>
    /// Configures the Perl application to use cpanm (App::cpanminus) as its package manager
    /// instead of the default cpan. Call this before <see cref="WithPackage{TResource}"/> to
    /// change how packages are installed.
    /// </summary>
    /// <typeparam name="TResource">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> WithCpanMinus<TResource>(
        this IResourceBuilder<TResource> builder) where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithAnnotation(new PerlPackageManagerAnnotation(PerlPackageManager.Cpanm),
            ResourceAnnotationMutationBehavior.Replace);

        builder.WithRequiredCommand("cpanm", context => Task.FromResult(
            IsCpanmAvailableForResource(context.ResolvedPath, builder)
                ? RequiredCommandValidationResult.Success()
                : RequiredCommandValidationResult.Failure(
                    "cpanm is not installed or not available. " +
                    "If using perlbrew, run 'perlbrew install-cpanm'.")),
            "https://cpanmin.us/");

        return builder;
    }

    private static bool IsCpanmAvailableForResource<TResource>(string resolvedPath, IResourceBuilder<TResource> builder)
        where TResource : PerlAppResource
    {
        if (builder.Resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var perlbrewAnnotation) &&
            perlbrewAnnotation.Environment is { } perlbrewEnv)
        {
            var perlbrewCpanmPath = perlbrewEnv.GetExecutable("cpanm");
            return File.Exists(perlbrewCpanmPath);
        }

        return IsCommandAvailable(resolvedPath);
    }

    private static bool IsCommandAvailable(string resolvedPath)
        => IsCommandAvailable(resolvedPath, Environment.GetEnvironmentVariable("PATH"), File.Exists);

    internal static bool IsCommandAvailable(string resolvedPath, string? pathValue, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return false;
        }

        if (fileExists(resolvedPath))
        {
            return true;
        }

        return TryResolveCommandFromPath(resolvedPath, pathValue, fileExists) is not null;
    }

    private static string? TryResolveCommandFromPath(string commandName)
        => TryResolveCommandFromPath(commandName, Environment.GetEnvironmentVariable("PATH"), File.Exists);

    internal static string? TryResolveCommandFromPath(string commandName, string? pathValue, Func<string, bool> fileExists)
    {
        var candidateName = Path.GetFileName(commandName);
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var candidates = new List<string> { candidateName };
        if (OperatingSystem.IsWindows())
        {
            var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT";
            foreach (var extension in pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!candidateName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add($"{candidateName}{extension}");
                }
            }
        }

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in candidates)
            {
                var fullPath = Path.Combine(directory, candidate);
                if (fileExists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Configures the Perl application to use Carton as its package manager.
    /// Carton manages dependencies via <c>cpanfile</c> and a lock file (<c>cpanfile.snapshot</c>),
    /// enabling reproducible builds. Use <see cref="WithProjectDependencies{TResource}"/> to
    /// run <c>carton install</c> at startup.
    /// </summary>
    /// <remarks>
    /// Carton only supports project-level dependency installation via <c>cpanfile</c>.
    /// Calling <see cref="WithPackage{TResource}"/> after <c>WithCarton()</c> will throw
    /// because Carton does not support installing individual modules.
    /// To install Carton, you may use "cpanm Carton".
    /// </remarks>
    /// <typeparam name="TResource">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> WithCarton<TResource>(
        this IResourceBuilder<TResource> builder) where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureCartonCanBeEnabled(builder);

        builder.WithAnnotation(new PerlPackageManagerAnnotation(PerlPackageManager.Carton),
            ResourceAnnotationMutationBehavior.Replace);

        builder.WithRequiredCommand("carton", context => Task.FromResult(
            IsCommandAvailable(context.ResolvedPath)
                ? RequiredCommandValidationResult.Success()
                : RequiredCommandValidationResult.Failure(
                    "Carton is not installed or not available on PATH. " +
                    "To install Carton, you may use \"cpanm Carton\".")),
            "https://metacpan.org/pod/Carton");

        TryAttachLocalLibEnvironmentToProjectInstaller(builder);

        return builder;
    }

    /// <summary>
    /// Adds a Perl package (module) to be installed before the application starts.
    /// Uses the configured package manager: cpan by default, or cpanm if
    /// <see cref="WithCpanMinus{TResource}"/> was called.
    /// </summary>
    /// <typeparam name="TResource">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="packageName">The name of the Perl module to install (e.g., "Mojolicious", "DBI").</param>
    /// <param name="force">If <c>true</c>, force installation even if the module is already installed or tests fail.</param>
    /// <param name="skipTest">If <c>true</c>, skip running tests during installation.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> WithPackage<TResource>(
        this IResourceBuilder<TResource> builder,
        string packageName,
        bool force = false,
        bool skipTest = false) where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(packageName);
        EnsurePackageInstallIsSupported(builder);

        builder.WithAnnotation(new PerlRequiredModuleAnnotation
        {
            Name = packageName,
            Force = force,
            SkipTest = skipTest
        });

        AddPackageInstaller(builder, packageName, force, skipTest);

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
    /// The path to the local::lib directory, relative to the application directory.
    /// Defaults to <c>"local"</c> (the Carton convention).
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> WithLocalLib<TResource>(
        this IResourceBuilder<TResource> builder,
        string path = "local") where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(path);

        builder.WithAnnotation(new PerlLocalLibAnnotation(path),
            ResourceAnnotationMutationBehavior.Replace);

        builder.WithEnvironment(context =>
        {
            var localLibPath = ResolveLocalLibPath(builder.Resource.WorkingDirectory, path);
            ApplyLocalLibEnvironment(context.EnvironmentVariables, localLibPath);
        });

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

        builder.WithCertificateTrustConfiguration(async context =>
        {
            if (context.CertificateBundlePath is { } bundlePath)
            {
                context.EnvironmentVariables["SSL_CERT_FILE"] = bundlePath;
                context.EnvironmentVariables["PERL_LWP_SSL_CA_FILE"] = bundlePath;
                context.EnvironmentVariables["MOJO_CA_FILE"] = bundlePath;
            }

            await Task.CompletedTask;
        });

        return builder;
    }

    /// <summary>
    /// Configures project-level dependency installation for the Perl application.
    /// Runs the appropriate install command based on the active package manager:
    /// <list type="bullet">
    /// <item>cpanm: <c>cpanm --installdeps --notest .</c></item>
    /// <item>carton: <c>carton install [--deployment]</c></item>
    /// </list>
    /// If the active package manager is <c>cpan</c> (the default), it is automatically
    /// switched to <c>cpanm</c> since <c>cpan</c> does not support <c>--installdeps</c>.
    /// </summary>
    /// <typeparam name="TResource">The type of the Perl application resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="cartonDeployment">
    /// If <c>true</c> and using Carton, adds <c>--deployment</c> for reproducible installs
    /// from <c>cpanfile.snapshot</c>. Ignored for other package managers.
    /// </param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<TResource> WithProjectDependencies<TResource>(
        this IResourceBuilder<TResource> builder,
        bool cartonDeployment = false) where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        // cpan doesn't support --installdeps; auto-switch to cpanm
        if (builder.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var pm) &&
            pm.PackageManager == PerlPackageManager.Cpan)
        {
            builder.WithCpanMinus();
        }

        AddProjectDependencyInstaller(builder, cartonDeployment);

        return builder;
    }

    /// <summary>
    /// Resolves the perl executable path, accounting for perlbrew environments.
    /// </summary>
    private static string ResolvePerlPath(PerlAppResource resource)
    {
        if (resource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var perlbrewAnnotation) &&
            perlbrewAnnotation.Environment is { } perlbrewEnv)
        {
            return perlbrewEnv.GetExecutable("perl");
        }

        return "perl";
    }

    /// <summary>
    /// Resolves the local::lib path from the resource's <see cref="PerlLocalLibAnnotation"/>,
    /// returning <c>null</c> when no local::lib is configured.
    /// </summary>
    private static string? ResolveLocalLibPath(PerlAppResource resource)
    {
        if (!resource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out var localLibAnnotation))
        {
            return null;
        }

        return ResolveLocalLibPath(resource.WorkingDirectory, localLibAnnotation.Path);
    }

    private static string ResolveLocalLibPath(string? workingDirectory, string configuredPath)
    {
        var appDir = workingDirectory ?? ".";
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(appDir, configuredPath));
    }

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

    private static string ResolveInstallerCommand(
        PerlAppResource parentResource,
        PerlPackageManagerAnnotation packageManager,
        IResourceBuilder<PerlModuleInstallerResource> installerBuilder)
    {
        if (!parentResource.TryGetLastAnnotation<PerlbrewEnvironmentAnnotation>(out var perlbrewAnnotation) ||
            perlbrewAnnotation.Environment is not { } perlbrewEnvironment)
        {
            return packageManager.ExecutableName;
        }

        installerBuilder.WithEnvironment(context =>
        {
            ApplyPerlbrewEnvironment(context.EnvironmentVariables, perlbrewEnvironment);
        });

        return perlbrewEnvironment.GetExecutable(packageManager.ExecutableName);
    }

    private static async Task<Dictionary<string, object>> BuildResourceEnvironmentVariablesAsync(
        PerlAppResource resource,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object> environmentVariables = new(StringComparer.Ordinal);
        EnvironmentCallbackContext context = new(executionContext, environmentVariables);

        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await callback.Callback(context).ConfigureAwait(false);
        }

        return environmentVariables;
    }

    private static void EnsurePackageInstallIsSupported<TResource>(IResourceBuilder<TResource> builder)
        where TResource : PerlAppResource
    {
        if (builder.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var packageManager) &&
            packageManager.PackageManager == PerlPackageManager.Carton)
        {
            throw new NotSupportedException(
                "WithPackage() and WithCarton() cannot be combined. " +
                "Carton manages all dependencies through your cpanfile. " +
                "Add the module to your cpanfile and call WithProjectDependencies() " +
                "to install all cpanfile dependencies at startup.");
        }
    }

    private static void EnsureCartonCanBeEnabled<TResource>(IResourceBuilder<TResource> builder)
        where TResource : PerlAppResource
    {
        if (builder.Resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Any() ||
            builder.Resource.Annotations.OfType<PerlModuleInstallerAnnotation>().Any())
        {
            throw new NotSupportedException(
                "WithPackage() and WithCarton() cannot be combined. " +
                "Carton manages all dependencies through your cpanfile. " +
                "Add the module to your cpanfile and call WithProjectDependencies() " +
                "to install all cpanfile dependencies at startup.");
        }
    }

    private static void AddInstaller<TResource>(IResourceBuilder<TResource> resource, string moduleName, bool install) where TResource : PerlAppResource
    {
        // Only install packages if in run mode
        if (resource.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            // Check if the installer resource already exists
            var installerName = $"{moduleName}-installer";
            resource.ApplicationBuilder.TryCreateResourceBuilder<PerlModuleInstallerResource>(installerName, out var existingResource);

            if (!install)
            {
                if (existingResource != null)
                {
                    // Remove existing installer resource if install is false
                    resource.ApplicationBuilder.Resources.Remove(existingResource.Resource);
                    resource.Resource.Annotations.OfType<WaitAnnotation>()
                        .Where(w => w.Resource == existingResource.Resource)
                        .ToList()
                        .ForEach(w => resource.Resource.Annotations.Remove(w));
                    resource.Resource.Annotations.OfType<PerlModuleInstallCommandAnnotation>()
                        .ToList()
                        .ForEach(a => resource.Resource.Annotations.Remove(a));
                }
                else
                {
                    // No installer needed
                }
                return;
            }

            if (existingResource is not null)
            {
                // Installer already exists
                return;
            }

            var installer = new PerlModuleInstallerResource(
                installerName.Replace(":", "8"), //limitation of aspire resource names not allowing colons, but perl module names often have colons, so replace with dashes for the installer resource name
                moduleName, 
                resource?.Resource.WorkingDirectory ?? throw new ArgumentNullException());
            var installerBuilder = resource.ApplicationBuilder.AddResource(installer)
                .WithParentRelationship(resource.Resource)
                .ExcludeFromManifest();

            resource.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>(async (_, ct) =>
            {
                // Preflight: skip installation if module is already available
                var perlPath = ResolvePerlPath(resource.Resource);
                var environmentVariables = await BuildResourceEnvironmentVariablesAsync(
                    resource.Resource,
                    resource.ApplicationBuilder.ExecutionContext,
                    ct).ConfigureAwait(false);

                if (await PerlModuleChecker.IsModuleInstalledAsync(perlPath, moduleName, environmentVariables, ct).ConfigureAwait(false))
                {
                    installerBuilder
                        .WithCommand(perlPath)
                        .WithWorkingDirectory(resource.Resource.WorkingDirectory)
                        .WithArgs("-e", "1");
                    return;
                }

                // set the installer's working directory to match the resource's working directory
                // and set the install command and args based on the resource's annotations
                if (!resource.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var packageManager) ||
                    !resource.Resource.TryGetLastAnnotation<PerlModuleInstallCommandAnnotation>(out var installCommand))
                {
                    throw new InvalidOperationException("PerlPackageManagerAnnotation and PerlModuleInstallCommandAnnotation are required when installing packages.");
                }

                var command = ResolveInstallerCommand(resource.Resource, packageManager, installerBuilder);

                installerBuilder
                    .WithCommand(command)
                    .WithWorkingDirectory(resource.Resource.WorkingDirectory)
                    .WithArgs(installCommand.Args);

                // Propagate local::lib environment to the installer so it installs to the right location
                var localLibPath = ResolveLocalLibPath(resource.Resource);
                if (localLibPath is not null)
                {
                    installerBuilder.WithEnvironment(context =>
                    {
                        ApplyLocalLibEnvironment(context.EnvironmentVariables, localLibPath);
                    });
                }
            });

            // Make the parent resource wait for the installer to complete
            resource.WaitForCompletion(installerBuilder);

            resource.WithAnnotation(new PerlModuleInstallerAnnotation(installer));

            // Eagerly propagate local::lib env vars if the annotation already exists
            TryAttachLocalLibToInstallerResource(installer, resource.Resource);
        }
    }

    /// <summary>
    /// Creates an installer child resource for a single package, deferring argument
    /// construction to <see cref="BeforeStartEvent"/> so the final package manager
    /// (cpan vs cpanm) is resolved at startup time.
    /// </summary>
    private static void AddPackageInstaller<TResource>(
        IResourceBuilder<TResource> resource,
        string packageName,
        bool force,
        bool skipTest) where TResource : PerlAppResource
    {
        // Only install packages if in run mode
        if (!resource.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return;
        }

        var installerName = $"{packageName}-installer";
        resource.ApplicationBuilder.TryCreateResourceBuilder<PerlModuleInstallerResource>(
            installerName, out var existingResource);

        if (existingResource is not null)
        {
            // Installer already exists for this package
            return;
        }

        var installer = new PerlModuleInstallerResource(
            installerName.Replace(":", "8"), //limitation of aspire resource names not allowing colons, but perl module names often have colons, so replace with dashes for the installer resource name
            packageName,
            resource.Resource.WorkingDirectory ?? throw new ArgumentNullException());

        var installerBuilder = resource.ApplicationBuilder
            .AddResource(installer)
            .WithParentRelationship(resource.Resource)
            .ExcludeFromManifest();

        resource.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>(async (_, ct) =>
        {
            // Preflight: skip installation if module is already available
            var perlPath = ResolvePerlPath(resource.Resource);
            var environmentVariables = await BuildResourceEnvironmentVariablesAsync(
                resource.Resource,
                resource.ApplicationBuilder.ExecutionContext,
                ct).ConfigureAwait(false);

            if (await PerlModuleChecker.IsModuleInstalledAsync(perlPath, packageName, environmentVariables, ct).ConfigureAwait(false))
            {
                installerBuilder
                    .WithCommand(perlPath)
                    .WithWorkingDirectory(resource.Resource.WorkingDirectory)
                    .WithArgs("-e", "1");
                return;
            }

            if (!resource.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var packageManager))
            {
                throw new InvalidOperationException(
                    "PerlPackageManagerAnnotation is required when installing packages. " +
                    "Ensure the resource was created via AddPerlScript or AddPerlApi.");
            }

            var command = ResolveInstallerCommand(resource.Resource, packageManager, installerBuilder);
            var localLibPath = ResolveLocalLibPath(resource.Resource);
            var installArgs = BuildInstallArgs(packageManager.PackageManager, packageName, force, skipTest, localLibPath);

            installerBuilder
                .WithCommand(command)
                .WithWorkingDirectory(resource.Resource.WorkingDirectory)
                .WithArgs(installArgs);

            // Propagate local::lib environment to the installer so it installs to the right location
            if (localLibPath is not null)
            {
                installerBuilder.WithEnvironment(context =>
                {
                    ApplyLocalLibEnvironment(context.EnvironmentVariables, localLibPath);
                });
            }
        });

        // Make the parent resource wait for the installer to complete
        resource.WaitForCompletion(installerBuilder);

        resource.WithAnnotation(new PerlModuleInstallerAnnotation(installer));

        // Eagerly propagate local::lib env vars if the annotation already exists
        TryAttachLocalLibToInstallerResource(installer, resource.Resource);
    }

    /// <summary>
    /// Creates a shared project-level dependency installer that runs <c>cpanm --installdeps .</c>
    /// or <c>carton install</c> based on the active package manager. Only one project installer
    /// is created per resource (idempotent via name-based check).
    /// </summary>
    private static void AddProjectDependencyInstaller<TResource>(
        IResourceBuilder<TResource> resource,
        bool cartonDeployment) where TResource : PerlAppResource
    {
        if (!resource.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return;
        }

        var installerName = $"{resource.Resource.Name}-deps-installer";
        resource.ApplicationBuilder.TryCreateResourceBuilder<PerlModuleInstallerResource>(
            installerName, out var existingResource);

        if (existingResource is not null)
        {
            return;
        }

        var installer = new PerlModuleInstallerResource(
            installerName,
            "project-deps",
            resource.Resource.WorkingDirectory ?? throw new ArgumentNullException());

        var installerBuilder = resource.ApplicationBuilder
            .AddResource(installer)
            .WithParentRelationship(resource.Resource)
            .ExcludeFromManifest();

        resource.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>(async (_, ct) =>
        {
            // Preflight: if a cpanfile exists, check whether all required modules are already installed
            var perlPath = ResolvePerlPath(resource.Resource);
            var environmentVariables = await BuildResourceEnvironmentVariablesAsync(
                resource.Resource,
                resource.ApplicationBuilder.ExecutionContext,
                ct).ConfigureAwait(false);
            var workingDir = resource.Resource.WorkingDirectory ?? ".";
            var cpanfilePath = Path.Combine(workingDir, "cpanfile");

            if (File.Exists(cpanfilePath))
            {
                var requiredModules = CpanfileParser.ParseRequiredModules(cpanfilePath);
                if (requiredModules.Count > 0)
                {
                    var allInstalled = true;
                    foreach (var module in requiredModules)
                    {
                        if (!await PerlModuleChecker.IsModuleInstalledAsync(perlPath, module, environmentVariables, ct).ConfigureAwait(false))
                        {
                            allInstalled = false;
                            break;
                        }
                    }

                    if (allInstalled)
                    {
                        installerBuilder
                            .WithCommand(perlPath)
                            .WithWorkingDirectory(workingDir)
                            .WithArgs("-e", "1");
                        return;
                    }
                }
            }

            if (!resource.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var packageManager))
            {
                throw new InvalidOperationException(
                    "PerlPackageManagerAnnotation is required for project dependency installation. " +
                    "Ensure the resource was created via AddPerlScript or AddPerlApi.");
            }

            var command = ResolveInstallerCommand(resource.Resource, packageManager, installerBuilder);
            var localLibPath = ResolveLocalLibPath(resource.Resource);
            var installArgs = BuildProjectInstallArgs(packageManager.PackageManager, cartonDeployment, localLibPath);

            installerBuilder
                .WithCommand(command)
                .WithWorkingDirectory(resource.Resource.WorkingDirectory ?? string.Empty)
                .WithArgs(installArgs);

            // Propagate local::lib environment to the installer so it installs to the right location
            if (localLibPath is not null)
            {
                installerBuilder.WithEnvironment(context =>
                {
                    ApplyLocalLibEnvironment(context.EnvironmentVariables, localLibPath);
                });
            }
        });

        resource.WaitForCompletion(installerBuilder);
        resource.WithAnnotation(new PerlProjectInstallerAnnotation(installer));

        TryAttachLocalLibEnvironmentToProjectInstaller(resource);
    }

    private static void ApplyLocalLibEnvironment(IDictionary<string, object> environmentVariables, string localLibPath)
    {
        var libPerl5 = Path.Combine(localLibPath, "lib", "perl5");

        environmentVariables["PERL5LIB"] = libPerl5;
        environmentVariables["PERL_LOCAL_LIB_ROOT"] = localLibPath;
        environmentVariables["PERL_MM_OPT"] = $"INSTALL_BASE={localLibPath}";
        environmentVariables["PERL_MB_OPT"] = $"--install_base {localLibPath}";
    }

    private static void TryAttachLocalLibEnvironmentToProjectInstaller<TResource>(
        IResourceBuilder<TResource> resource) where TResource : PerlAppResource
    {
        if (!resource.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var packageManager) ||
            packageManager.PackageManager != PerlPackageManager.Carton)
        {
            return;
        }

        if (!resource.Resource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out _) ||
            !resource.Resource.TryGetLastAnnotation<PerlProjectInstallerAnnotation>(out var projectInstallerAnnotation))
        {
            return;
        }

        TryAttachLocalLibToInstallerResource(projectInstallerAnnotation.Resource, resource.Resource);
    }

    /// <summary>
    /// Attaches local::lib environment variables to a single installer resource.
    /// Uses <see cref="PerlLocalLibInstallerEnvironmentAnnotation"/> as a guard to prevent duplicates.
    /// </summary>
    private static void TryAttachLocalLibToInstallerResource(
        ExecutableResource installerResource,
        PerlAppResource parentResource)
    {
        if (!parentResource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out var localLibAnnotation))
        {
            return;
        }

        if (installerResource.Annotations.OfType<PerlLocalLibInstallerEnvironmentAnnotation>().Any())
        {
            return;
        }

        installerResource.Annotations.Add(new PerlLocalLibInstallerEnvironmentAnnotation());

        installerResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            var localLibPath = ResolveLocalLibPath(parentResource.WorkingDirectory, localLibAnnotation.Path);
            ApplyLocalLibEnvironment(context.EnvironmentVariables, localLibPath);
            return Task.CompletedTask;
        }));
    }

    /// <summary>
    /// Eagerly propagates local::lib environment to all existing installer child resources.
    /// Called from <see cref="WithLocalLib{TResource}"/> to handle the case where installers
    /// were created before <c>WithLocalLib</c> in the fluent chain.
    /// </summary>
    private static void TryAttachLocalLibToExistingInstallers<TResource>(
        IResourceBuilder<TResource> resource) where TResource : PerlAppResource
    {
        if (!resource.Resource.TryGetLastAnnotation<PerlLocalLibAnnotation>(out _))
        {
            return;
        }

        foreach (var moduleAnnotation in resource.Resource.Annotations.OfType<PerlModuleInstallerAnnotation>())
        {
            TryAttachLocalLibToInstallerResource(moduleAnnotation.Resource, resource.Resource);
        }

        if (resource.Resource.TryGetLastAnnotation<PerlProjectInstallerAnnotation>(out var projectAnnotation))
        {
            TryAttachLocalLibToInstallerResource(projectAnnotation.Resource, resource.Resource);
        }
    }

    /// <summary>
    /// Builds the correct CLI arguments for installing a package based on the package manager.
    /// </summary>
    /// <remarks>
    /// <para>cpanm: <c>cpanm [--force] [--notest] PackageName</c></para>
    /// <para>cpan:  <c>cpan [-f] [-T] [-i] PackageName</c> (where <c>-i</c> is required when <c>-f</c> is used)</para>
    /// <para>carton: not supported for individual packages — throws.</para>
    /// </remarks>
    internal static string[] BuildInstallArgs(PerlPackageManager packageManager, string packageName, bool force, bool skipTest, string? localLibPath = null)
    {
        var args = new List<string>();

        switch (packageManager)
        {
            case PerlPackageManager.Cpanm:
                if (localLibPath is not null)
                {
                    args.Add("--local-lib");
                    args.Add(localLibPath);
                }
                if (force) args.Add("--force");
                if (skipTest) args.Add("--notest");
                args.Add(packageName);
                break;

            case PerlPackageManager.Cpan:
                if (force) args.Add("-f");
                if (skipTest) args.Add("-T");
                if (force) args.Add("-i");
                args.Add(packageName);
                break;

            case PerlPackageManager.Carton:
                throw new NotSupportedException(
                    "WithPackage() and WithCarton() cannot be combined. " +
                    "Carton manages all dependencies through your cpanfile. " +
                    "Add the module to your cpanfile and call WithProjectDependencies() " +
                    "to install all cpanfile dependencies at startup.");

            default:
                throw new NotSupportedException(
                    $"Package manager '{packageManager}' is not supported. Use 'cpan' (default) or call WithCpanMinus() for 'cpanm'.");
        }

        return args.ToArray();
    }

    /// <summary>
    /// Builds the CLI arguments for project-level dependency installation.
    /// </summary>
    /// <remarks>
    /// <para>cpanm: <c>cpanm --installdeps --notest .</c></para>
    /// <para>carton: <c>carton install [--deployment]</c></para>
    /// <para>cpan: not supported for project-level installs — throws.</para>
    /// </remarks>
    internal static string[] BuildProjectInstallArgs(PerlPackageManager packageManager, bool cartonDeployment, string? localLibPath = null)
    {
        return packageManager switch
        {
            PerlPackageManager.Cpanm => localLibPath is not null
                ? ["--local-lib", localLibPath, "--installdeps", "--notest", "."]
                : ["--installdeps", "--notest", "."],
            PerlPackageManager.Carton => cartonDeployment
                ? ["install", "--deployment"]
                : ["install"],
            PerlPackageManager.Cpan => throw new NotSupportedException(
                "cpan does not support project-level dependency installation. " +
                "Call WithCpanMinus() or WithCarton() first."),
            _ => throw new NotSupportedException(
                $"Package manager '{packageManager}' is not supported for project-level installs."),
        };
    }

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
    /// Checks whether the application directory contains standard Perl dependency files
    /// (<c>cpanfile</c>, <c>Makefile.PL</c>, or <c>Build.PL</c>).
    /// </summary>
    internal static bool HasDependencyFiles(string appDirectory, string appHostDirectory)
    {
        var resolvedDir = Path.IsPathRooted(appDirectory)
            ? appDirectory
            : Path.GetFullPath(appDirectory, appHostDirectory);

        return File.Exists(Path.Combine(resolvedDir, "cpanfile"))
            || File.Exists(Path.Combine(resolvedDir, "Makefile.PL"))
            || File.Exists(Path.Combine(resolvedDir, "Build.PL"));
    }

    /// <summary>
    /// Configures the Perl resource for publish (deployment) mode by adding
    /// <c>PublishAsDockerFile</c> and a Dockerfile builder callback that generates
    /// a Dockerfile tailored to the active package manager.
    /// </summary>
    private static void ConfigurePublishMode(
        IResourceBuilder<PerlAppResource> resourceBuilder,
        EntrypointType entrypointType,
        string entrypoint,
        string? apiSubcommand)
    {
        resourceBuilder.PublishAsDockerFile();

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

            switch (packageManager)
            {
                case PerlPackageManager.Carton:
                    BuildCartonDockerfile(context.Builder, entrypointType, entrypoint, apiSubcommand, baseImage, buildImage);
                    break;

                default:
                    BuildCpanmDockerfile(context.Builder, entrypointType, entrypoint, apiSubcommand, baseImage);
                    break;
            }

            return Task.CompletedTask;
        }));
    }

    /// <summary>
    /// Builds a single-stage Dockerfile using cpanm for dependency installation.
    /// </summary>
    internal static void BuildCpanmDockerfile(
        DockerfileBuilder builder,
        EntrypointType entrypointType,
        string entrypoint,
        string? apiSubcommand,
        string baseImage)
    {
        var entrypointArgs = BuildContainerEntrypointArguments(entrypointType, entrypoint, apiSubcommand, useLocalLibPath: false);

        builder.From(baseImage)
            .WorkDir("/app")
            .Run("cpanm App::cpanminus || true")
            .Copy("cpanfile", "./")
            .Run("cpanm --installdeps --notest .")
            .Copy(".", ".")
            .Entrypoint(entrypointArgs);
    }

    /// <summary>
    /// Builds a multi-stage Dockerfile using Carton for reproducible dependency resolution.
    /// </summary>
    internal static void BuildCartonDockerfile(
        DockerfileBuilder builder,
        EntrypointType entrypointType,
        string entrypoint,
        string? apiSubcommand,
        string runtimeImage,
        string buildImage)
    {
        var entrypointArgs = BuildContainerEntrypointArguments(entrypointType, entrypoint, apiSubcommand, useLocalLibPath: true);

        builder.From(buildImage, "build")
            .WorkDir("/app")
            .Run("cpanm Carton")
            .Copy("cpanfile", "./")
            .Copy("cpanfile.snapshot", "./")
            .Run("carton install --deployment")
            .Copy(".", ".");

        builder.From(runtimeImage)
            .WorkDir("/app")
            .CopyFrom("build", "/app", "/app")
            .Entrypoint(entrypointArgs);
    }
}
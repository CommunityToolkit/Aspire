using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Package manager selection, installer resource creation, and CLI argument building
/// for Perl application resources.
/// </summary>
public static partial class PerlAppResourceBuilderExtensions
{
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

#pragma warning disable ASPIRECOMMAND001
        builder.WithRequiredCommand("cpanm", context => Task.FromResult(
            IsCpanmAvailableForResource(context.ResolvedPath, builder)
                ? RequiredCommandValidationResult.Success()
                : RequiredCommandValidationResult.Failure(
                    "cpanm is not installed or not available. " +
                    "If using perlbrew, run 'perlbrew install-cpanm'.")),
            "https://cpanmin.us/");
#pragma warning restore ASPIRECOMMAND001

        return builder;
    }

    /// <summary>
    /// Determines whether <c>cpanm</c> is available for the target resource,
    /// preferring the perlbrew-managed executable when perlbrew is configured.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="resolvedPath">The resolved command path passed by required-command validation.</param>
    /// <param name="builder">The resource builder used to inspect annotations.</param>
    /// <returns><c>true</c> when <c>cpanm</c> can be resolved; otherwise, <c>false</c>.</returns>
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

    /// <summary>
    /// Checks whether a command path or command name can be resolved from the
    /// current process <c>PATH</c>.
    /// </summary>
    /// <param name="resolvedPath">The command path or command name.</param>
    /// <returns><c>true</c> when the command is available; otherwise, <c>false</c>.</returns>
    private static bool IsCommandAvailable(string resolvedPath)
        => IsCommandAvailable(resolvedPath, Environment.GetEnvironmentVariable("PATH"), File.Exists);

    /// <summary>
    /// Checks whether a command path or command name can be resolved using a
    /// supplied PATH value and file existence probe.
    /// </summary>
    /// <param name="resolvedPath">The command path or command name.</param>
    /// <param name="pathValue">The PATH value to search.</param>
    /// <param name="fileExists">A file-existence predicate for testability.</param>
    /// <returns><c>true</c> when the command is available; otherwise, <c>false</c>.</returns>
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

    /// <summary>
    /// Attempts to resolve a command name from the current process <c>PATH</c>.
    /// </summary>
    /// <param name="commandName">The command name to resolve.</param>
    /// <returns>The full resolved path when found; otherwise, <c>null</c>.</returns>
    private static string? TryResolveCommandFromPath(string commandName)
        => TryResolveCommandFromPath(commandName, Environment.GetEnvironmentVariable("PATH"), File.Exists);

    /// <summary>
    /// Attempts to resolve a command name from the specified PATH value.
    /// On Windows, PATHEXT extensions are considered during resolution.
    /// </summary>
    /// <param name="commandName">The command name to resolve.</param>
    /// <param name="pathValue">The PATH value to search.</param>
    /// <param name="fileExists">A file-existence predicate for testability.</param>
    /// <returns>The full resolved path when found; otherwise, <c>null</c>.</returns>
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
    /// <seealso cref="WithProjectDependencies{TResource}"/>
    public static IResourceBuilder<TResource> WithCarton<TResource>(
        this IResourceBuilder<TResource> builder) where TResource : PerlAppResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        EnsureCartonCanBeEnabled(builder);

        builder.WithAnnotation(new PerlPackageManagerAnnotation(PerlPackageManager.Carton),
            ResourceAnnotationMutationBehavior.Replace);

#pragma warning disable ASPIRECOMMAND001
        builder.WithRequiredCommand("carton", context => Task.FromResult(
            IsCommandAvailable(context.ResolvedPath)
                ? RequiredCommandValidationResult.Success()
                : RequiredCommandValidationResult.Failure(
                    "Carton is not installed or not available on PATH. " +
                    "To install Carton, you may use \"cpanm Carton\".")),
            "https://metacpan.org/pod/Carton");
#pragma warning restore ASPIRECOMMAND001

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
    /// <remarks>
    /// A child installer resource is created that runs before the main application starts.
    /// If the module is already installed (verified via <c>perl -MModuleName -e 1</c>),
    /// installation is skipped. The installer uses the active package manager —
    /// <c>cpan</c> by default, or <c>cpanm</c> if <see cref="WithCpanMinus{TResource}"/>
    /// was called.
    /// </remarks>
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
    /// <remarks>
    /// Expects a <c>cpanfile</c> in the resource's working directory (<c>appDirectory</c>).
    /// If the cpanfile is in a different location, adjust <c>appDirectory</c> accordingly.
    /// When using Carton with <paramref name="cartonDeployment"/> set to <c>true</c>,
    /// a <c>cpanfile.snapshot</c> must also be present.
    /// </remarks>
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

        builder.WithAnnotation(new PerlCartonDeploymentAnnotation(cartonDeployment));

        AddProjectDependencyInstaller(builder, cartonDeployment);

        // When using Carton with --deployment, validate that cpanfile.snapshot exists
        if (cartonDeployment &&
            builder.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var currentPm) &&
            currentPm.PackageManager == PerlPackageManager.Carton)
        {
            var workingDir = builder.Resource.WorkingDirectory ?? ".";
            var snapshotPath = Path.Combine(workingDir, "cpanfile.snapshot");
            if (!File.Exists(snapshotPath))
            {
#pragma warning disable ASPIRECOMMAND001
                builder.WithRequiredCommand("cpanfile.snapshot", _ => Task.FromResult(
                    RequiredCommandValidationResult.Failure(
                        "carton install --deployment requires a cpanfile.snapshot file. " +
                        "Run 'carton install' first to generate the snapshot, then commit it to source control.")),
                    "https://metacpan.org/pod/Carton");
#pragma warning restore ASPIRECOMMAND001
            }
        }

        return builder;
    }

    /// <summary>
    /// Resolves the installer command executable and applies perlbrew environment
    /// variables to the installer when perlbrew is configured.
    /// </summary>
    /// <param name="parentResource">The parent Perl application resource.</param>
    /// <param name="packageManager">The selected package manager annotation.</param>
    /// <param name="installerBuilder">The installer resource builder to configure.</param>
    /// <returns>The command executable path to use for installation.</returns>
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

    /// <summary>
    /// Validates that per-package installation is compatible with the current
    /// package-manager mode.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="builder">The resource builder to validate.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when Carton mode is enabled and package-level installation is requested.
    /// </exception>
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

    /// <summary>
    /// Validates that Carton can be enabled for the resource.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="builder">The resource builder to validate.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when package-level installers are already configured.
    /// </exception>
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

        var installerName = $"{resource.Resource.Name}-{packageName}-installer";
        resource.ApplicationBuilder.TryCreateResourceBuilder<PerlModuleInstallerResource>(
            installerName, out var existingResource);

        if (existingResource is not null)
        {
            // Installer already exists for this package
            return;
        }

        var installer = new PerlModuleInstallerResource(
            installerName.Replace(":", "8"), // replace colons with '8' for the installer resource name (Aspire resource names cannot contain ':')
            packageName,
            resource.Resource.WorkingDirectory ?? throw new ArgumentNullException());

        var installerBuilder = resource.ApplicationBuilder
            .AddResource(installer)
            .WithParentRelationship(resource.Resource)
            .ExcludeFromManifest();

        resource.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>(async (evt, ct) =>
        {
            var logger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource.Resource);

            if (!resource.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var packageManager))
            {
                throw new InvalidOperationException(
                    "PerlPackageManagerAnnotation is required when installing packages. " +
                    "Ensure the resource was created via AddPerlScript or AddPerlApi.");
            }

            var command = ResolveInstallerCommand(resource.Resource, packageManager, installerBuilder);
            var localLibPath = ResolveLocalLibPath(resource.Resource);
            var installArgs = BuildInstallArgs(packageManager.PackageManager, packageName, force, skipTest, localLibPath);

            logger.LogInformation("Installing module '{PackageName}' via {PackageManager}: {Command} {Args}",
                packageName, packageManager.PackageManager, command, string.Join(" ", installArgs));
            if (localLibPath is not null)
            {
                logger.LogInformation("Install target (local::lib): {LocalLibPath}", localLibPath);
            }

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

        // Eagerly propagate certificate trust env vars if configured
        TryAttachCertificateTrustToInstallerResource(installer, resource.Resource);
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

        resource.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>(async (evt, ct) =>
        {
            var logger = evt.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource.Resource);

            if (!resource.Resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var packageManager))
            {
                throw new InvalidOperationException(
                    "PerlPackageManagerAnnotation is required for project dependency installation. " +
                    "Ensure the resource was created via AddPerlScript or AddPerlApi.");
            }

            var command = ResolveInstallerCommand(resource.Resource, packageManager, installerBuilder);
            var localLibPath = ResolveLocalLibPath(resource.Resource);
            var installArgs = BuildProjectInstallArgs(packageManager.PackageManager, cartonDeployment, localLibPath);

            logger.LogInformation("Installing project dependencies via {PackageManager}: {Command} {Args}",
                packageManager.PackageManager, command, string.Join(" ", installArgs));
            logger.LogInformation("Working directory: {WorkingDirectory}", resource.Resource.WorkingDirectory ?? string.Empty);
            if (localLibPath is not null)
            {
                logger.LogInformation("Install target (local::lib): {LocalLibPath}", localLibPath);
            }

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

        // Eagerly propagate certificate trust env vars if configured
        TryAttachCertificateTrustToInstallerResource(installer, resource.Resource);
    }

    /// <summary>
    /// Applies local::lib environment propagation to the project dependency
    /// installer when Carton mode and a project installer are both present.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="resource">The parent Perl resource builder.</param>
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
    /// <param name="installerResource">The installer resource that receives local::lib variables.</param>
    /// <param name="parentResource">The parent Perl application resource containing local::lib configuration.</param>
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
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="resource">The parent Perl resource builder.</param>
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
    /// Applies SSL/TLS certificate trust environment variables to a single installer resource.
    /// Uses <see cref="PerlCertificateTrustAnnotation"/> on the parent as a guard —
    /// only propagates when cert trust is enabled. Skips if already attached.
    /// </summary>
    /// <param name="installerResource">The installer resource that receives certificate variables.</param>
    /// <param name="parentResource">The parent Perl application resource with certificate trust configuration.</param>
    private static void TryAttachCertificateTrustToInstallerResource(
        ExecutableResource installerResource,
        PerlAppResource parentResource)
    {
        if (!parentResource.Annotations.OfType<PerlCertificateTrustAnnotation>().Any())
        {
            return;
        }

        if (installerResource.Annotations.OfType<PerlCertificateTrustAnnotation>().Any())
        {
            return;
        }

        installerResource.Annotations.Add(new PerlCertificateTrustAnnotation());

        installerResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            // Propagate by reading from the process environment — Aspire sets these
            // on the host process when certificate trust is configured.
            var sslCertFile = Environment.GetEnvironmentVariable("SSL_CERT_FILE");
            if (sslCertFile is not null)
            {
                context.EnvironmentVariables["SSL_CERT_FILE"] = sslCertFile;
                context.EnvironmentVariables["PERL_LWP_SSL_CA_FILE"] = sslCertFile;
                context.EnvironmentVariables["MOJO_CA_FILE"] = sslCertFile;
            }

            return Task.CompletedTask;
        }));
    }

    /// <summary>
    /// Propagates certificate trust to all existing installer child resources.
    /// Called from <see cref="WithPerlCertificateTrust{TResource}"/> to handle the case
    /// where installers were created before <c>WithPerlCertificateTrust</c> in the fluent chain.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    /// <param name="resource">The parent Perl resource builder.</param>
    private static void TryAttachCertificateTrustToExistingInstallers<TResource>(
        IResourceBuilder<TResource> resource) where TResource : PerlAppResource
    {
        foreach (var moduleAnnotation in resource.Resource.Annotations.OfType<PerlModuleInstallerAnnotation>())
        {
            TryAttachCertificateTrustToInstallerResource(moduleAnnotation.Resource, resource.Resource);
        }

        if (resource.Resource.TryGetLastAnnotation<PerlProjectInstallerAnnotation>(out var projectAnnotation))
        {
            TryAttachCertificateTrustToInstallerResource(projectAnnotation.Resource, resource.Resource);
        }
    }

    /// <summary>
    /// Builds the correct CLI arguments for installing a package based on the package manager.
    /// </summary>
    /// <remarks>
    /// <para> For cpanm: <c>cpanm [--force] [--notest] PackageName</c> </para>
    /// <para> For cpan:  <c>cpan [-f] [-T] [-i] PackageName</c> (where <c>-i</c> is to dodge interactivity) </para>
    /// <para> For carton: not supported for individual packages — throws. </para>
    /// </remarks>
    /// <param name="packageManager">The package manager to build arguments for.</param>
    /// <param name="packageName">The module/package name to install.</param>
    /// <param name="force">Whether to force installation.</param>
    /// <param name="skipTest">Whether to skip module tests during installation.</param>
    /// <param name="localLibPath">Optional local::lib path used for cpanm installs.</param>
    /// <returns>The argument list passed to the selected package manager.</returns>
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
                args.Add("-i");
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
    /// Sanitizes installer resource names for Aspire model constraints.
    /// Perl module names commonly include <c>::</c>; Aspire resource names cannot include
    /// <c>:</c>, so this convention maps each colon to <c>8</c> to preserve readability
    /// and stable name generation.
    /// </summary>
    /// <param name="installerName">The original installer resource name.</param>
    /// <returns>A sanitized installer resource name valid for Aspire resource constraints.</returns>
    private static string SanitizeInstallerResourceName(string installerName)
        => installerName.Replace(":", "8", StringComparison.Ordinal);

    /// <summary>
    /// Builds the CLI arguments for project-level dependency installation.
    /// </summary>
    /// <remarks>
    /// <para>cpanm: <c>cpanm --installdeps --notest .</c></para>
    /// <para>carton: <c>carton install [--deployment]</c></para>
    /// <para>cpan: not supported for project-level installs — throws.</para>
    /// </remarks>
    /// <param name="packageManager">The package manager to build arguments for.</param>
    /// <param name="cartonDeployment">Whether to use Carton deployment mode.</param>
    /// <param name="localLibPath">Optional local::lib path used for cpanm installs.</param>
    /// <returns>The argument list passed to the selected package manager.</returns>
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
}

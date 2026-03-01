using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Neon;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for configuring <see cref="NeonProjectResource"/> builders.
/// </summary>
public static class NeonResourceBuilderExtensions
{
    // ──────────────────── Project ────────────────────

    /// <summary>
    /// Configures the project by its Neon project ID (an existing project).
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="projectId">The Neon project ID.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithProjectId(
        this IResourceBuilder<NeonProjectResource> builder,
        string projectId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        builder.Resource.Options.ProjectId = projectId;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Configures the project by name. The project must already exist in Neon.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="projectName">The Neon project name.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithProjectName(
        this IResourceBuilder<NeonProjectResource> builder,
        string projectName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(projectName);

        builder.Resource.Options.ProjectName = projectName;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Creates a Neon project if it does not already exist.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="projectName">The Neon project name to find or create.</param>
    /// <param name="regionId">The Neon region ID for the project.</param>
    /// <param name="postgresVersion">The PostgreSQL version.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> AddProject(
        this IResourceBuilder<NeonProjectResource> builder,
        string projectName,
        string? regionId = null,
        int? postgresVersion = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(projectName);

        var options = builder.Resource.Options;
        options.ProjectName = projectName;
        options.CreateProjectIfMissing = true;

        if (!string.IsNullOrWhiteSpace(regionId))
        {
            options.RegionId = regionId;
        }

        if (postgresVersion.HasValue)
        {
            options.PostgresVersion = postgresVersion;
        }

        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    // ──────────────────── Organization ────────────────────

    /// <summary>
    /// Targets a specific Neon organization by its ID.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="organizationId">The Neon organization ID.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithOrganizationId(
        this IResourceBuilder<NeonProjectResource> builder,
        string organizationId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        builder.Resource.Options.OrganizationId = organizationId;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Targets a specific Neon organization by its name.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="organizationName">The Neon organization name.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithOrganizationName(
        this IResourceBuilder<NeonProjectResource> builder,
        string organizationName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(organizationName);

        builder.Resource.Options.OrganizationName = organizationName;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    // ──────────────────── Branch ────────────────────

    /// <summary>
    /// Connects to an existing Neon branch by its ID.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="branchId">The Neon branch ID.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithBranchId(
        this IResourceBuilder<NeonProjectResource> builder,
        string branchId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(branchId);

        builder.Resource.Options.Branch.BranchId = branchId;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Connects to an existing Neon branch by its name. The branch must already exist.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="branchName">The Neon branch name.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithBranchName(
        this IResourceBuilder<NeonProjectResource> builder,
        string branchName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(branchName);

        builder.Resource.Options.Branch.BranchName = branchName;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Creates a Neon branch if it does not already exist.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="branchName">The branch name to find or create.</param>
    /// <param name="endpointType">The type of endpoint to create with the branch.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> AddBranch(
        this IResourceBuilder<NeonProjectResource> builder,
        string branchName,
        NeonEndpointType endpointType = NeonEndpointType.ReadWrite)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(branchName);

        var branch = builder.Resource.Options.Branch;
        branch.BranchName = branchName;
        branch.CreateBranchIfMissing = true;
        branch.CreateEndpointIfMissing = true;
        branch.EndpointType = endpointType;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Creates an ephemeral branch for each application run. The branch is deleted on shutdown.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="prefix">Optional prefix for ephemeral branch names.</param>
    /// <param name="endpointType">The type of endpoint to create with the branch.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> AddEphemeralBranch(
        this IResourceBuilder<NeonProjectResource> builder,
        string? prefix = null,
        NeonEndpointType endpointType = NeonEndpointType.ReadWrite)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var branch = builder.Resource.Options.Branch;
        branch.UseEphemeralBranch = true;
        branch.CreateBranchIfMissing = true;
        branch.CreateEndpointIfMissing = true;
        branch.EndpointType = endpointType;

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            branch.EphemeralBranchPrefix = prefix;
        }

        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    // ──────────────────── Branch features ────────────────────

    /// <summary>
    /// Enables branch restore (refresh) from a source branch on each application run.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">Optional callback to configure restore options.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithBranchRestore(
        this IResourceBuilder<NeonProjectResource> builder,
        Action<NeonBranchRestoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.Options.Branch.Restore.Enabled = true;
        configure?.Invoke(builder.Resource.Options.Branch.Restore);
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Configures the branch to be created with anonymized (masked) data using PostgreSQL Anonymizer.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">Callback to configure masking rules and anonymization behavior.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithAnonymizedData(
        this IResourceBuilder<NeonProjectResource> builder,
        Action<NeonAnonymizationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = builder.Resource.Options.Branch.Anonymization;
        options.Enabled = true;
        configure(options);
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Sets the branch as the project's default branch after provisioning.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> AsDefaultBranch(
        this IResourceBuilder<NeonProjectResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.Options.Branch.SetAsDefault = true;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    // ──────────────────── Connection options ────────────────────

    /// <summary>
    /// Configures the default database name used in connection strings.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="databaseName">The database name.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithDatabaseName(
        this IResourceBuilder<NeonProjectResource> builder,
        string databaseName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        builder.Resource.Options.DatabaseName = databaseName;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Configures the default role name used in connection strings.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="roleName">The role name.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithRoleName(
        this IResourceBuilder<NeonProjectResource> builder,
        string roleName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(roleName);

        builder.Resource.Options.RoleName = roleName;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Enables connection pooling (PgBouncer) for connection strings.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithConnectionPooler(
        this IResourceBuilder<NeonProjectResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.Options.UseConnectionPooler = true;
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    private static void RefreshProvisionerConfiguration(IResourceBuilder<NeonProjectResource> builder)
    {
        if (builder.Resource.ProvisionerResource is null)
        {
            return;
        }

        _ = EnsureProvisioner(builder);
    }

    /// <summary>
    /// Configures Neon to use existing project and branch resources without creating missing artifacts.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <returns>The same Neon resource builder for fluent chaining.</returns>
    public static IResourceBuilder<NeonProjectResource> AsExisting(
        this IResourceBuilder<NeonProjectResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        SetExistingProvisioningDefaults(builder.Resource.Options);
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    internal static IResourceBuilder<NeonProjectResource> EnsureProvisioner(
        this IResourceBuilder<NeonProjectResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        NeonProvisionerIntent mode = ResolveProvisionerMode(builder.Resource.Options);
        _ = AddNeonProvisioner(builder, $"{builder.Resource.Name}-provisioner", mode);
        return builder;
    }

    /// <summary>
    /// Configures Neon infrastructure options using a callback.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <param name="configure">The infrastructure configuration callback.</param>
    /// <returns>The same Neon resource builder for fluent chaining.</returns>
    public static IResourceBuilder<NeonProjectResource> ConfigureInfrastructure(
        this IResourceBuilder<NeonProjectResource> builder,
        Action<NeonProjectOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        configure(builder.Resource.Options);
        RefreshProvisionerConfiguration(builder);
        return builder;
    }

    /// <summary>
    /// Gets the resource builder for the underlying Neon provisioner project resource.
    /// Returns <see langword="null"/> when the provisioner is not a <see cref="ProjectResource"/> (for example, in run mode).
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <returns>An <see cref="IResourceBuilder{T}"/> for the provisioner <see cref="ProjectResource"/>, or <see langword="null"/>.</returns>
    public static IResourceBuilder<ProjectResource>? GetProvisionerBuilder(
        this IResourceBuilder<NeonProjectResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = EnsureProvisioner(builder);

        if (builder.Resource.ProvisionerResource is ProjectResource projectProvisioner)
        {
            return builder.ApplicationBuilder.CreateResourceBuilder(projectProvisioner);
        }

        return null;
    }

    private static IResourceBuilder<IResourceWithWaitSupport> AddNeonProvisioner(
        this IResourceBuilder<NeonProjectResource> builder,
        [ResourceName] string name,
        NeonProvisionerIntent mode)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (builder.Resource.ProvisionerResource is IResourceWithWaitSupport existingProvisioner)
        {
            if (existingProvisioner is not IResource existingResource ||
                !string.Equals(existingResource.Name, name, StringComparison.Ordinal))
            {
                throw new DistributedApplicationException(
                    $"Neon provisioner is already configured. Custom provisioner name '{name}' is not supported after AddNeon().");
            }

            var existingHostOutputFilePath = builder.Resource.TryGetLastAnnotation<NeonExternalProvisionerAnnotation>(out var existingAnnotation)
                ? existingAnnotation.OutputFilePath
                : ResolveHostOutputFilePath(builder.ApplicationBuilder, builder.Resource.Name);

            var existingProvisionerOutputFilePath = ResolveProvisionerOutputPath(builder.ApplicationBuilder, builder.Resource.Name);

            DeleteProvisionerArtifacts(existingHostOutputFilePath);

            var existingProjectPath = builder.Resource.TryGetLastAnnotation<NeonExternalProvisionerAnnotation>(out existingAnnotation)
                ? existingAnnotation.ProjectPath
                : NeonProvisionerProjectTemplate.EnsureProject(builder.ApplicationBuilder);

            if (existingProvisioner is ProjectResource existingProjectResource)
            {
                var existingProjectBuilder = builder.ApplicationBuilder.CreateResourceBuilder(existingProjectResource);
                ApplyProvisionerEnvironment(existingProjectBuilder, builder.Resource, mode, existingProvisionerOutputFilePath);
                builder.WaitForCompletion(existingProjectBuilder);
                builder.WithAnnotation(new NeonExternalProvisionerAnnotation(existingProjectBuilder.Resource, existingProjectPath, existingHostOutputFilePath, mode));
                return existingProjectBuilder;
            }

            if (existingProvisioner is NeonProvisionerExecutableResource existingExecutableResource)
            {
                var existingExecutableBuilder = builder.ApplicationBuilder.CreateResourceBuilder(existingExecutableResource);
                ApplyProvisionerEnvironment(existingExecutableBuilder, builder.Resource, mode, existingProvisionerOutputFilePath);
                builder.WithAnnotation(new NeonExternalProvisionerAnnotation(existingExecutableBuilder.Resource, existingProjectPath, existingHostOutputFilePath, mode));
                return existingExecutableBuilder;
            }

            throw new DistributedApplicationException("Unsupported Neon provisioner resource type.");
        }

        var provisionerProjectPath = NeonProvisionerProjectTemplate.EnsureProject(builder.ApplicationBuilder);

        var outputFilePath = ResolveProvisionerOutputPath(builder.ApplicationBuilder, builder.Resource.Name);
        DeleteProvisionerArtifacts(outputFilePath);

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            var executableProvisioner = builder.ApplicationBuilder
                .AddResource(new NeonProvisionerExecutableResource(name, Path.GetDirectoryName(provisionerProjectPath)!))
                .WithArgs("run", "--project", provisionerProjectPath);

            executableProvisioner.WithParentRelationship(builder.Resource);

            ApplyProvisionerEnvironment(executableProvisioner, builder.Resource, mode, outputFilePath);

            builder.WithAnnotation(new NeonExternalProvisionerAnnotation(executableProvisioner.Resource, provisionerProjectPath, outputFilePath, mode));

            builder.Resource.HostOutputDirectory = Path.GetDirectoryName(outputFilePath);

            builder.Resource.ProvisionerResource = executableProvisioner.Resource;
            return executableProvisioner;
        }

        var volumeName = $"{SanitizeForFileName(builder.Resource.Name)}-neon-output";

        var projectProvisioner = builder.ApplicationBuilder
            .AddProject(name, provisionerProjectPath)
            .WithAnnotation(new ContainerMountAnnotation(volumeName, NeonOutputMountPath, ContainerMountType.Volume, isReadOnly: false))
            .WithParentRelationship(builder.Resource);

        ApplyProvisionerEnvironment(projectProvisioner, builder.Resource, mode, outputFilePath);

        builder.WaitForCompletion(projectProvisioner);
        builder.WithAnnotation(new NeonExternalProvisionerAnnotation(projectProvisioner.Resource, provisionerProjectPath, outputFilePath, mode));

        builder.Resource.OutputVolumeName = volumeName;

        builder.Resource.ProvisionerResource = projectProvisioner.Resource;
        return projectProvisioner;
    }

    private static NeonProvisionerIntent ResolveProvisionerMode(NeonProjectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        NeonBranchOptions branch = options.Branch;
        bool requiresProvisioning =
            options.CreateProjectIfMissing ||
            branch.CreateBranchIfMissing ||
            branch.CreateEndpointIfMissing ||
            branch.UseEphemeralBranch ||
            branch.Restore.Enabled ||
            branch.Anonymization.Enabled;

        return requiresProvisioning
            ? NeonProvisionerIntent.Provision
            : NeonProvisionerIntent.Attach;
    }

    private static void SetExistingProvisioningDefaults(NeonProjectOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.CreateProjectIfMissing = false;

        NeonBranchOptions branch = options.Branch;
        branch.CreateBranchIfMissing = false;
        branch.CreateEndpointIfMissing = false;
        branch.UseEphemeralBranch = false;

    }

    /// <summary>
    /// The container mount path used for provisioner output in the shared volume pattern.
    /// </summary>
    private const string NeonOutputMountPath = "/neon-output";

    /// <summary>
    /// Returns the path where the provisioner should write output, as seen from
    /// INSIDE the provisioner process/container. In run mode this is a host path
    /// (the provisioner is a host process). In publish mode this is a container-
    /// internal path that maps to the bind-mounted host directory.
    /// </summary>
    private static string ResolveProvisionerOutputPath(IDistributedApplicationBuilder builder, string resourceName)
    {
        string safeResourceName = SanitizeForFileName(resourceName);

        if (!builder.ExecutionContext.IsRunMode)
        {
            return $"{NeonOutputMountPath}/{safeResourceName}.json";
        }

        string appHostFingerprint = ComputeHash(builder.AppHostDirectory);

        string runOutputDiscriminator = builder.Resources
            .OfType<NeonProjectResource>()
            .FirstOrDefault(resource => string.Equals(resource.Name, resourceName, StringComparison.Ordinal))?
            .Options
            .Provisioning
            .RunOutputDiscriminator
            ?? Guid.NewGuid().ToString("N")[..12];

        NeonProjectResource? projectResource = builder.Resources
            .OfType<NeonProjectResource>()
            .FirstOrDefault(resource => string.Equals(resource.Name, resourceName, StringComparison.Ordinal));

        if (projectResource is not null && string.IsNullOrWhiteSpace(projectResource.Options.Provisioning.RunOutputDiscriminator))
        {
            projectResource.Options.Provisioning.RunOutputDiscriminator = runOutputDiscriminator;
        }

        string outputDirectory = Path.Combine(Path.GetTempPath(), "aspire-neon-output", appHostFingerprint, runOutputDiscriminator);
        Directory.CreateDirectory(outputDirectory);
        return Path.Combine(outputDirectory, $"{safeResourceName}.json");
    }

    /// <summary>
    /// Returns the provisioner output file path on the HOST filesystem.
    /// In run mode this is the same as <see cref="ResolveProvisionerOutputPath"/>.
    /// In publish mode the provisioner writes to a container-internal path, but a
    /// bind mount maps it back to this host path.
    /// </summary>
    private static string ResolveHostOutputFilePath(IDistributedApplicationBuilder builder, string resourceName)
    {
        string safeResourceName = SanitizeForFileName(resourceName);
        string appHostFingerprint = ComputeHash(builder.AppHostDirectory);

        string runOutputDiscriminator = builder.Resources
            .OfType<NeonProjectResource>()
            .FirstOrDefault(resource => string.Equals(resource.Name, resourceName, StringComparison.Ordinal))?
            .Options
            .Provisioning
            .RunOutputDiscriminator
            ?? Guid.NewGuid().ToString("N")[..12];

        NeonProjectResource? projectResource = builder.Resources
            .OfType<NeonProjectResource>()
            .FirstOrDefault(resource => string.Equals(resource.Name, resourceName, StringComparison.Ordinal));

        if (projectResource is not null && string.IsNullOrWhiteSpace(projectResource.Options.Provisioning.RunOutputDiscriminator))
        {
            projectResource.Options.Provisioning.RunOutputDiscriminator = runOutputDiscriminator;
        }

        string outputDirectory = Path.Combine(Path.GetTempPath(), "aspire-neon-output", appHostFingerprint, runOutputDiscriminator);
        Directory.CreateDirectory(outputDirectory);
        return Path.Combine(outputDirectory, $"{safeResourceName}.json");
    }

    private static string ComputeHash(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA256.HashData(bytes);
        string hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hash[..16];
    }

    private static string SanitizeForFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            builder.Append(invalidChars.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }

    private static void DeleteProvisionerArtifacts(string outputFilePath)
    {
        DeleteIfExists(outputFilePath);
        DeleteIfExists($"{outputFilePath}.error.log");
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void ApplyProvisionerEnvironment<TResource>(
        IResourceBuilder<TResource> provisioner,
        NeonProjectResource resource,
        NeonProvisionerIntent mode,
        string outputFilePath)
        where TResource : class, IResourceWithWaitSupport, IResourceWithEnvironment
    {
        NeonProjectOptions options = resource.Options;
        NeonBranchOptions branch = options.Branch;

        string maskingRulesJson = JsonSerializer.Serialize(
            branch.Anonymization.MaskingRules.Select(rule => new
            {
                rule.DatabaseName,
                rule.SchemaName,
                rule.TableName,
                rule.ColumnName,
                rule.MaskingFunction,
                rule.MaskingValue,
            }));

        string databaseSpecsJson = JsonSerializer.Serialize(
            resource.Databases.Values
                .Select(database => new NeonProvisionerDatabaseSpec
                {
                    ResourceName = database.Name,
                    DatabaseName = database.DatabaseName,
                    RoleName = database.RoleName,
                })
                .ToArray());

        provisioner
            .WithEnvironment(NeonProvisionerEnvironmentVariables.ApiKey, ReferenceExpression.Create($"{resource.ApiKeyParameter}"))
            .WithEnvironment(NeonProvisionerEnvironmentVariables.Mode, mode switch
            {
                NeonProvisionerIntent.Attach => "attach",
                NeonProvisionerIntent.Provision => "provision",
                _ => throw new DistributedApplicationException($"Unsupported Neon provisioner intent '{mode}'.")
            })
            .WithEnvironment(NeonProvisionerEnvironmentVariables.OutputFilePath, outputFilePath)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.ProjectId, options.ProjectId ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.ProjectName, options.ProjectName ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.CreateProjectIfMissing, options.CreateProjectIfMissing.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.RegionId, options.RegionId ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.PostgresVersion, options.PostgresVersion?.ToString() ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.OrganizationId, options.OrganizationId ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.OrganizationName, options.OrganizationName ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchId, branch.BranchId ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchName, branch.BranchName ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.ParentBranchId, branch.ParentBranchId ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.ParentBranchName, branch.ParentBranchName ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchProtected, branch.Protected?.ToString() ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchInitSource, branch.InitSource switch
            {
                NeonBranchInitSource.SchemaOnly => "schema-only",
                NeonBranchInitSource.ParentData => "parent-data",
                _ => string.Empty,
            })
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchExpiresAt, branch.ExpiresAt?.ToString("O") ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchParentLsn, branch.ParentLsn ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchParentTimestamp, branch.ParentTimestamp?.ToString("O") ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchArchived, branch.Archived?.ToString() ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.CreateBranchIfMissing, branch.CreateBranchIfMissing.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchSetAsDefault, branch.SetAsDefault.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.UseEphemeralBranch, branch.UseEphemeralBranch.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.EphemeralBranchPrefix, branch.EphemeralBranchPrefix)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchRestoreEnabled, branch.Restore.Enabled.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchRestoreSourceBranchId, branch.Restore.SourceBranchId ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchRestoreSourceLsn, branch.Restore.SourceLsn ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchRestoreSourceTimestamp, branch.Restore.SourceTimestamp?.ToString("O") ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchRestorePreserveUnderName, branch.Restore.PreserveUnderName ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchAnonymizationEnabled, branch.Anonymization.Enabled.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchAnonymizationStart, branch.Anonymization.StartAnonymization.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.BranchMaskingRulesJson, maskingRulesJson)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.EndpointId, branch.EndpointId ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.EndpointType, branch.EndpointType == NeonEndpointType.ReadOnly ? "read_only" : "read_write")
            .WithEnvironment(NeonProvisionerEnvironmentVariables.CreateEndpointIfMissing, branch.CreateEndpointIfMissing.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.DatabaseName, options.DatabaseName ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.RoleName, options.RoleName ?? string.Empty)
            .WithEnvironment(NeonProvisionerEnvironmentVariables.UseConnectionPooler, options.UseConnectionPooler.ToString())
            .WithEnvironment(NeonProvisionerEnvironmentVariables.DatabaseSpecsJson, databaseSpecsJson);
    }

}

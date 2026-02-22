using System.Text.Json;
using System.Linq.Expressions;
using System.Reflection;
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

    /// <summary>
    /// Configures project-level options using a callback.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">The options configuration callback.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithProjectOptions(
        this IResourceBuilder<NeonProjectResource> builder,
        Action<NeonProjectOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        configure(builder.Resource.Options);
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

    /// <summary>
    /// Configures branch options using a callback.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">The branch options configuration callback.</param>
    /// <returns>The updated resource builder.</returns>
    public static IResourceBuilder<NeonProjectResource> WithBranchOptions(
        this IResourceBuilder<NeonProjectResource> builder,
        Action<NeonBranchOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        configure(builder.Resource.Options.Branch);
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

        _ = builder.AddProvisioner(builder.Resource.Options.Provisioning.Mode);
    }

    /// <summary>
    /// Adds an external one-shot provisioner project using the default name pattern <c>{neonResourceName}-provisioner</c>.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <param name="mode">
    /// The provisioner execution mode. Defaults to <see cref="NeonProvisionerMode.Attach"/>.
    /// </param>
    /// <returns>The same Neon resource builder for fluent chaining.</returns>
    public static IResourceBuilder<NeonProjectResource> AddProvisioner(
        this IResourceBuilder<NeonProjectResource> builder,
        NeonProvisionerMode mode = NeonProvisionerMode.Attach)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.AddNeonProvisioner($"{builder.Resource.Name}-provisioner", mode);
        return builder;
    }

    /// <summary>
    /// Adds an external one-shot provisioner project with a custom resource name.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <param name="name">The provisioner project resource name.</param>
    /// <param name="mode">
    /// The provisioner execution mode. Defaults to <see cref="NeonProvisionerMode.Attach"/>.
    /// </param>
    /// <returns>The same Neon resource builder for fluent chaining.</returns>
    public static IResourceBuilder<NeonProjectResource> AddProvisioner(
        this IResourceBuilder<NeonProjectResource> builder,
        [ResourceName] string name,
        NeonProvisionerMode mode = NeonProvisionerMode.Attach)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        _ = builder.AddNeonProvisioner(name, mode);
        return builder;
    }

    /// <summary>
    /// Configures container build options for the underlying Neon provisioner project resource.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <param name="configure">The callback used to configure container build options.</param>
    /// <returns>The same Neon resource builder for fluent chaining.</returns>
    public static IResourceBuilder<NeonProjectResource> WithContainerBuildOptions(
        this IResourceBuilder<NeonProjectResource> builder,
        Action<dynamic> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        _ = builder.AddNeonProvisioner(
            $"{builder.Resource.Name}-provisioner",
            builder.Resource.Options.Provisioning.Mode);

        if (builder.Resource.ProvisionerResource is not ProjectResource projectProvisioner)
        {
            return builder;
        }

        var provisionerBuilder = builder.ApplicationBuilder.CreateResourceBuilder(projectProvisioner);

        ForwardContainerBuildOptions(provisionerBuilder, configure);
        return builder;
    }

    private static void ForwardContainerBuildOptions(
        IResourceBuilder<ProjectResource> provisionerBuilder,
        Action<dynamic> configure)
    {
        Assembly aspireAssembly = typeof(ProjectResource).Assembly;
        Type? resourceExtensionsType = aspireAssembly.GetType("Aspire.Hosting.ApplicationModel.ResourceExtensions");

        MethodInfo? genericMethod = resourceExtensionsType?
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
                method.Name == "WithContainerBuildOptions"
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == 1
                && method.GetParameters().Length == 2);

        if (genericMethod is null)
        {
            throw new DistributedApplicationException(
                "Unable to locate Aspire WithContainerBuildOptions API for ProjectResource. Ensure your Aspire.Hosting package supports project container build options.");
        }

        MethodInfo closedMethod = genericMethod.MakeGenericMethod(typeof(ProjectResource));
        ParameterInfo callbackParameter = closedMethod.GetParameters()[1];
        Type callbackType = callbackParameter.ParameterType;
        Type optionsType = callbackType.GenericTypeArguments[0];
        Delegate callbackDelegate = BuildDynamicCallback(callbackType, optionsType, configure);

        _ = closedMethod.Invoke(null, [provisionerBuilder, callbackDelegate]);
    }

    private static Delegate BuildDynamicCallback(
        Type callbackType,
        Type optionsType,
        Action<dynamic> configure)
    {
        ParameterExpression optionsParameter = Expression.Parameter(optionsType, "options");
        ConstantExpression configureConstant = Expression.Constant(configure);
        MethodInfo invoke = typeof(Action<dynamic>).GetMethod(nameof(Action<dynamic>.Invoke))!;

        UnaryExpression optionsAsObject = Expression.Convert(optionsParameter, typeof(object));
        MethodCallExpression call = Expression.Call(configureConstant, invoke, optionsAsObject);
        LambdaExpression lambda = Expression.Lambda(callbackType, call, optionsParameter);
        return lambda.Compile();
    }

    /// <summary>
    /// Adds an external one-shot provisioner project and wires Neon to wait for its completion.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <param name="name">The provisioner project resource name.</param>
    /// <param name="mode">
    /// The provisioner execution mode. Defaults to <see cref="NeonProvisionerMode.Attach"/>.
    /// Use <see cref="NeonProvisionerMode.Provision"/> to create missing resources before attaching.
    /// </param>
    /// <returns>A builder for the provisioner project resource.</returns>
    public static IResourceBuilder<IResourceWithWaitSupport> AddNeonProvisioner(
        this IResourceBuilder<NeonProjectResource> builder,
        [ResourceName] string name,
        NeonProvisionerMode mode = NeonProvisionerMode.Attach)
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

            var existingOutputFilePath = builder.Resource.TryGetLastAnnotation<NeonExternalProvisionerAnnotation>(out var existingAnnotation)
                ? existingAnnotation.OutputFilePath
                : ResolveProvisionerOutputPath(builder.ApplicationBuilder, builder.Resource.Name);

            DeleteProvisionerArtifacts(existingOutputFilePath);

            var existingProjectPath = builder.Resource.TryGetLastAnnotation<NeonExternalProvisionerAnnotation>(out existingAnnotation)
                ? existingAnnotation.ProjectPath
                : NeonProvisionerProjectTemplate.EnsureProject(builder.ApplicationBuilder);

            if (existingProvisioner is ProjectResource existingProjectResource)
            {
                var existingProjectBuilder = builder.ApplicationBuilder.CreateResourceBuilder(existingProjectResource);
                ApplyProvisionerEnvironment(existingProjectBuilder, builder.Resource, mode, existingOutputFilePath);
                builder.WaitForCompletion(existingProjectBuilder);
                builder.WithAnnotation(new NeonExternalProvisionerAnnotation(existingProjectBuilder.Resource, existingProjectPath, existingOutputFilePath, mode));

                builder.Resource.Options.Provisioning.Mode = mode;
                return existingProjectBuilder;
            }

            if (existingProvisioner is NeonProvisionerExecutableResource existingExecutableResource)
            {
                var existingExecutableBuilder = builder.ApplicationBuilder.CreateResourceBuilder(existingExecutableResource);
                ApplyProvisionerEnvironment(existingExecutableBuilder, builder.Resource, mode, existingOutputFilePath);
                builder.WithAnnotation(new NeonExternalProvisionerAnnotation(existingExecutableBuilder.Resource, existingProjectPath, existingOutputFilePath, mode));

                builder.Resource.Options.Provisioning.Mode = mode;
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

            builder.Resource.ProvisionerResource = executableProvisioner.Resource;
            builder.Resource.Options.Provisioning.Mode = mode;
            return executableProvisioner;
        }

        var projectProvisioner = builder.ApplicationBuilder
            .AddProject(name, provisionerProjectPath)
            .WithParentRelationship(builder.Resource);

        ApplyProvisionerEnvironment(projectProvisioner, builder.Resource, mode, outputFilePath);

        builder.WaitForCompletion(projectProvisioner);
        builder.WithAnnotation(new NeonExternalProvisionerAnnotation(projectProvisioner.Resource, provisionerProjectPath, outputFilePath, mode));

        builder.Resource.ProvisionerResource = projectProvisioner.Resource;
        builder.Resource.Options.Provisioning.Mode = mode;
        return projectProvisioner;
    }

    private static string ResolveProvisionerOutputPath(IDistributedApplicationBuilder builder, string resourceName)
    {
        string appHostFingerprint = ComputeHash(builder.AppHostDirectory);
        string safeResourceName = SanitizeForFileName(resourceName);

        if (!builder.ExecutionContext.IsRunMode)
        {
            return $"/tmp/aspire-neon-output/{appHostFingerprint}/{safeResourceName}.json";
        }

        string outputDirectory = Path.Combine(Path.GetTempPath(), "aspire-neon-output", appHostFingerprint);
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
        NeonProvisionerMode mode,
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
            .WithEnvironment(NeonProvisionerEnvironmentVariables.Mode, mode == NeonProvisionerMode.Attach ? "attach" : "provision")
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

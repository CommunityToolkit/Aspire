#pragma warning disable ASPIREPIPELINES001 // Pipeline APIs are experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NuGet.Versioning;
using System.Diagnostics.CodeAnalysis;

[assembly: Aspire.Hosting.AspireExport(typeof(Aspire.Hosting.DacpacSource))]

namespace Aspire.Hosting;

/// <summary>
/// Specifies where a DACPAC pipeline operation obtains its DACPAC.
/// </summary>
public enum DacpacSource
{
    /// <summary>
    /// Builds the SQL project and uses the resulting DACPAC.
    /// </summary>
    Build,

    /// <summary>
    /// Uses a DACPAC path supplied through the pipeline parameter.
    /// </summary>
    File,

    /// <summary>
    /// Uses the DACPAC already produced at the configured SQL project output path.
    /// </summary>
    Existing
}

/// <summary>
/// Configures the DACPAC pipeline operations for a SQL Server Database Project resource.
/// </summary>
[AspireDto]
public sealed record DacpacPipelineOptions
{
    /// <summary>
    /// Gets whether publishing should include the resource-specific DACPAC publish step.
    /// </summary>
    public bool? IncludeInPublish { get; init; }

    /// <summary>
    /// Gets whether deployment should include the resource-specific DACPAC deploy step.
    /// </summary>
    public bool? IncludeInDeploy { get; init; }

    /// <summary>
    /// Gets the source used by DACPAC pipeline operations.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="DacpacSource.Build"/> for SQL project resources, independently of
    /// <see cref="IncludeInPublish"/> and <see cref="IncludeInDeploy"/>. Not supported for package resources.
    /// </remarks>
    public DacpacSource? Source { get; init; }

    /// <summary>
    /// Gets the MSBuild configuration used by the pipeline build step.
    /// </summary>
    public string? BuildConfiguration { get; init; }

    /// <summary>
    /// Gets the directory used for generated report and script artifacts.
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Gets the Microsoft.SqlPackage version used by the pipeline.
    /// </summary>
    public string? SqlPackageVersion { get; init; }

    /// <summary>
    /// Gets the optional name of the parameter containing the target connection string.
    /// </summary>
    public string? TargetConnectionStringParameterName { get; init; }
}

/// <summary>
/// Provides methods for configuring DACPAC pipeline operations.
/// </summary>
public static class SqlProjectPipelineExtensions
{
    /// <summary>
    /// Enables composable DACPAC pipeline steps for a SQL Server Database Project resource.
    /// </summary>
    /// <param name="builder">The SQL Server Database Project resource builder.</param>
    /// <param name="options">The DACPAC pipeline options.</param>
    /// <returns>The resource builder for further configuration.</returns>
    [AspireExport]
    [Experimental("CTASPIREPIPELINES001", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
    public static IResourceBuilder<SqlProjectResource> WithPipelineOptions(
        this IResourceBuilder<SqlProjectResource> builder,
        DacpacPipelineOptions? options = null)
        => InternalWithPipelineOptions(builder, options);

    /// <summary>
    /// Enables composable DACPAC pipeline steps for a SQL Server Database Project package resource.
    /// </summary>
    /// <typeparam name="TPackage">The package metadata type for the SQL Server Database Project package.</typeparam>
    /// <param name="builder">The SQL Server Database Project package resource builder.</param>
    /// <param name="options">The DACPAC pipeline options.</param>
    /// <returns>The resource builder for further configuration.</returns>
    /// <remarks>This method is not available in polyglot app hosts.</remarks>
    [AspireExportIgnore(Reason = "The generic package metadata type is not available in polyglot app hosts.")]
    [Experimental("CTASPIREPIPELINES001", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
    public static IResourceBuilder<SqlPackageResource<TPackage>> WithPipelineOptions<TPackage>(
        this IResourceBuilder<SqlPackageResource<TPackage>> builder,
        DacpacPipelineOptions? options = null)
        where TPackage : IPackageMetadata
        => InternalWithPipelineOptions(builder, options);

    // AddSqlProject calls this so the resource has pipeline steps even without WithPipelineOptions.
    internal static IResourceBuilder<TResource> AddDefaultPipelineSteps<TResource>(
        IResourceBuilder<TResource> builder)
        where TResource : IResourceWithDacpac
        => InternalWithPipelineOptions(builder, null);

    private static IResourceBuilder<TResource> InternalWithPipelineOptions<TResource>(
        IResourceBuilder<TResource> builder,
        DacpacPipelineOptions? options)
        where TResource : IResourceWithDacpac
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder;
        }

        var previous = builder.Resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var annotation)
            ? annotation.Options
            : DacpacPipelineEffectiveOptions.Default;
        var merged = DacpacPipelineConfiguration.Merge(
            builder.Resource,
            builder.ApplicationBuilder.Configuration,
            previous,
            options);

        builder.WithAnnotation(
            new DacpacPipelineOptionsAnnotation(merged),
            ResourceAnnotationMutationBehavior.Replace);

        // The path parameter exists only while the final source is File, so a later call that
        // switches source does not leave an unused parameter for ProcessParameters to prompt for.
        var hasPathParameter = builder.Resource.TryGetLastAnnotation<DacpacPipelinePathParameterAnnotation>(out var pathParameter);
        if (merged.Source == DacpacSource.File && !hasPathParameter)
        {
            var parameterName = $"{builder.Resource.Name}-dacpac-path";
            var parameter = builder.ApplicationBuilder.AddParameter(parameterName)
                .WithDescription($"Path to the DACPAC used by the pipeline operations for '{builder.Resource.Name}'.");
            builder.WithAnnotation(new DacpacPipelinePathParameterAnnotation(parameter.Resource));
        }
        else if (merged.Source != DacpacSource.File && hasPathParameter)
        {
            builder.ApplicationBuilder.Resources.Remove(pathParameter!.Parameter);
            builder.Resource.Annotations.Remove(pathParameter);
        }

        builder.ApplicationBuilder.Services.TryAddSingleton<SqlProjectDacpacResolver>();
        builder.ApplicationBuilder.Services.TryAddSingleton<ISqlPackageRunner, SqlPackageRunner>();
        builder.ApplicationBuilder.Services.TryAddSingleton<SqlPackageTargetLock>();

        if (!builder.Resource.HasAnnotationOfType<DacpacPipelineRegistrationAnnotation>())
        {
            // Steps are created per resource, while deploy ordering and local target parameters
            // span all SQL resources: only the first SQL resource registers that shared callback.
            var isFirstSqlResource = !builder.ApplicationBuilder.Resources
                .OfType<IResourceWithDacpac>()
                .Any(resource => resource.HasAnnotationOfType<DacpacPipelineRegistrationAnnotation>());
            builder.WithAnnotation(new DacpacPipelineRegistrationAnnotation());
            if (isFirstSqlResource)
            {
                builder.WithPipelineConfiguration(context =>
                {
                    ConfigureLocalTargetParameters(builder.ApplicationBuilder, context.Model);
                    SqlProjectPipelineSteps.WireAggregateDeploySteps(context.Steps);
                });
            }

            builder.WithAnnotation(new PipelineStepAnnotation(context =>
            {
                // Read the options when the pipeline runs: WithPipelineOptions can follow AddSqlProject.
                if (!builder.Resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var current))
                {
                    throw new DistributedApplicationException(
                        $"DACPAC pipeline options for resource '{builder.Resource.Name}' are unavailable.");
                }

                SqlProjectPipelineSteps.ValidateSecretTargetParameter(
                    context.PipelineContext.Model,
                    current.Options.TargetConnectionStringParameterName);
                return SqlProjectPipelineSteps.CreateSteps(builder.Resource, current.Options);
            }));
        }

        return builder;
    }

    private static void ConfigureLocalTargetParameters(IDistributedApplicationBuilder builder, DistributedApplicationModel model)
    {
        // Both fluent calls have run; register only the fallbacks in the final model.
        foreach (var resource in model.Resources.OfType<IResourceWithDacpac>().ToArray())
        {
            if (!resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options)
                || options.Options.TargetConnectionStringParameterName is not null
                || !resource.TryGetLastAnnotation<DacpacTargetAnnotation>(out var target)
                || target.Target is not SqlServerDatabaseResource
                || target.PipelineConnectionStringParameter is not null)
            {
                continue;
            }

            var name = $"{target.Target.Name}-connection-string";
            var parameter = SqlProjectPipelineSteps.FindSecretTargetParameter(model, name)
                ?? builder.AddParameter(name, secret: true)
                    .WithDescription($"Connection string used by DACPAC pipeline operations targeting '{target.Target.Name}'.")
                    .Resource;

            resource.Annotations.Remove(target);
            resource.Annotations.Add(target with { PipelineConnectionStringParameter = parameter });
        }
    }
}

/// <summary>
/// Prevents duplicate pipeline registration for a DACPAC resource.
/// </summary>
internal sealed class DacpacPipelineRegistrationAnnotation : IResourceAnnotation
{
}

/// <summary>
/// Stores the late-bound parameter providing an existing DACPAC path.
/// </summary>
/// <param name="Parameter">The DACPAC path parameter.</param>
internal sealed record DacpacPipelinePathParameterAnnotation(ParameterResource Parameter) : IResourceAnnotation;

/// <summary>
/// Stores the effective DACPAC pipeline configuration for a resource.
/// </summary>
/// <param name="Options">The immutable effective options.</param>
internal sealed record DacpacPipelineOptionsAnnotation(DacpacPipelineEffectiveOptions Options) : IResourceAnnotation;

/// <summary>
/// Represents the merged DACPAC pipeline configuration.
/// </summary>
/// <param name="IncludeInPublish">Whether publish includes the DACPAC step.</param>
/// <param name="IncludeInDeploy">Whether deploy includes the DACPAC step.</param>
/// <param name="Source">The configured DACPAC source, when applicable.</param>
/// <param name="BuildConfiguration">The pipeline build configuration.</param>
/// <param name="OutputDirectory">The report and script output directory.</param>
/// <param name="SqlPackageVersion">The Microsoft.SqlPackage version.</param>
/// <param name="TargetConnectionStringParameterName">The optional target parameter name.</param>
internal sealed record DacpacPipelineEffectiveOptions(
    bool IncludeInPublish,
    bool IncludeInDeploy,
    DacpacSource? Source,
    string BuildConfiguration,
    string OutputDirectory,
    string SqlPackageVersion,
    string? TargetConnectionStringParameterName)
{
    public static DacpacPipelineEffectiveOptions Default { get; } = new(
        IncludeInPublish: false,
        IncludeInDeploy: false,
        Source: null,
        BuildConfiguration: "Release",
        OutputDirectory: Path.Combine("artifacts", "dacpac"),
        SqlPackageVersion: "170.3.93",
        TargetConnectionStringParameterName: null);
}

/// <summary>
/// Merges and validates DACPAC pipeline configuration.
/// </summary>
internal static class DacpacPipelineConfiguration
{
    /// <summary>
    /// Applies code options, then <c>DacpacPipeline:&lt;resource&gt;:*</c> configuration, over the previous options.
    /// </summary>
    /// <remarks>Configuration wins so CI can change the behavior without editing the AppHost.</remarks>
    internal static DacpacPipelineEffectiveOptions Merge(
        IResourceWithDacpac resource,
        IConfiguration configuration,
        DacpacPipelineEffectiveOptions previous,
        DacpacPipelineOptions? options)
    {
        DacpacPipelineOptions? configured;
        try
        {
            configured = configuration.GetSection($"DacpacPipeline:{resource.Name}").Get<DacpacPipelineOptions>();
        }
        catch (InvalidOperationException ex)
        {
            throw new DistributedApplicationException(
                $"DACPAC pipeline configuration for resource '{resource.Name}' is invalid. {ex.Message}", ex);
        }

        var merged = Apply(Apply(previous, options), configured);
        if (SqlProjectPipelineSteps.IsPackageResource(resource))
        {
            SqlProjectPipelineSteps.RejectPackageSourceOverride(resource, merged.Source);
        }

        if (merged.Source is { } source && !Enum.IsDefined(source))
        {
            throw new DistributedApplicationException($"DACPAC pipeline source '{source}' is invalid.");
        }

        // dnx downloads and runs this package, so only an exact, pinned version is accepted.
        if (!NuGetVersion.TryParse(merged.SqlPackageVersion, out var version))
        {
            throw new DistributedApplicationException(
                $"DACPAC pipeline option 'SqlPackageVersion' must be an exact NuGet version, not '{merged.SqlPackageVersion}'.");
        }

        return merged with { SqlPackageVersion = version.ToNormalizedString() };
    }

    private static DacpacPipelineEffectiveOptions Apply(
        DacpacPipelineEffectiveOptions current,
        DacpacPipelineOptions? options)
        => options is null
            ? current
            : new(
                options.IncludeInPublish ?? current.IncludeInPublish,
                options.IncludeInDeploy ?? current.IncludeInDeploy,
                options.Source ?? current.Source,
                RejectBlank(options.BuildConfiguration, nameof(options.BuildConfiguration)) ?? current.BuildConfiguration,
                RejectBlank(options.OutputDirectory, nameof(options.OutputDirectory)) ?? current.OutputDirectory,
                RejectBlank(options.SqlPackageVersion, nameof(options.SqlPackageVersion)) ?? current.SqlPackageVersion,
                RejectBlank(options.TargetConnectionStringParameterName, nameof(options.TargetConnectionStringParameterName))
                    ?? current.TargetConnectionStringParameterName);

    // Null keeps the current value; an explicitly blank value is a configuration mistake.
    private static string? RejectBlank(string? value, string name)
        => value is not null && string.IsNullOrWhiteSpace(value)
            ? throw new DistributedApplicationException($"DACPAC pipeline option '{name}' cannot be empty.")
            : value;
}

/// <summary>
/// Creates and configures DACPAC pipeline steps.
/// </summary>
internal static class SqlProjectPipelineSteps
{
    internal static void WireAggregateDeploySteps(IReadOnlyList<PipelineStep> steps)
    {
        var provisionSteps = steps
            .Where(step => step.Tags.Contains(WellKnownPipelineTags.ProvisionInfrastructure))
            .ToList();
        var deploySteps = steps
            .Where(step => step.Resource is IResourceWithDacpac resource
                && step.Name == $"deploy-{resource.Name}")
            .ToList();
        var aggregateRoots = deploySteps
            .Where(step =>
                step.Resource is IResourceWithDacpac resource
                && resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options)
                && options.Options.IncludeInDeploy)
            .ToList();
        // Opted-in deployments pull in their SQL WaitFor prerequisites, even when those
        // prerequisites are not themselves opted in. Manual deploy steps outside this graph stay independent.
        var closure = new HashSet<PipelineStep>();
        var pending = new Stack<PipelineStep>(aggregateRoots);
        var deployByResource = deploySteps.ToDictionary(step => step.Resource!.Name, StringComparer.Ordinal);

        while (pending.TryPop(out var step))
        {
            if (!closure.Add(step))
            {
                continue;
            }

            foreach (var provisionStep in provisionSteps)
            {
                DependOn(step, provisionStep.Name);
            }

            if (step.Resource!.TryGetAnnotationsOfType<WaitAnnotation>(out var waits))
            {
                foreach (var wait in waits)
                {
                    if (wait.Resource is IResourceWithDacpac
                        && deployByResource.TryGetValue(wait.Resource.Name, out var prerequisite))
                    {
                        DependOn(step, prerequisite.Name);
                        pending.Push(prerequisite);
                    }
                }
            }
        }

        // Order all targets together before adding serialization edges: two deployments on the
        // same database may already be ordered indirectly through a deployment on another database.
        foreach (var group in OrderDeploySteps(closure).GroupBy(GetTargetKey, StringComparer.Ordinal))
        {
            PipelineStep? previous = null;
            foreach (var current in group)
            {
                if (previous is not null)
                {
                    DependOn(current, previous.Name);
                }
                previous = current;
            }
        }
    }

    private static List<PipelineStep> OrderDeploySteps(HashSet<PipelineStep> deploySteps)
    {
        var names = deploySteps.Select(step => step.Name).ToHashSet(StringComparer.Ordinal);
        var indegree = deploySteps.ToDictionary(
            step => step,
            step => step.DependsOnSteps.Count(names.Contains));
        var dependents = deploySteps
            .SelectMany(step => step.DependsOnSteps.Where(names.Contains).Select(name => (name, step)))
            .ToLookup(pair => pair.name, pair => pair.step, StringComparer.Ordinal);

        var ready = new SortedSet<PipelineStep>(
            deploySteps.Where(step => indegree[step] == 0),
            Comparer<PipelineStep>.Create((left, right) =>
                StringComparer.Ordinal.Compare(left.Name, right.Name)));
        var ordered = new List<PipelineStep>(deploySteps.Count);
        while (ready.Count > 0)
        {
            var current = ready.Min!;
            ready.Remove(current);
            ordered.Add(current);

            foreach (var dependent in dependents[current.Name])
            {
                indegree[dependent]--;
                if (indegree[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (ordered.Count != deploySteps.Count)
        {
            throw new DistributedApplicationException("DACPAC deployment dependencies contain a cycle.");
        }

        return ordered;
    }

    private static string GetTargetKey(PipelineStep step)
    {
        if (step.Resource is not IResourceWithDacpac resource)
        {
            return $"step:{step.Name}";
        }

        var parameterName = resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options)
            ? options.Options.TargetConnectionStringParameterName
            : null;
        if (!string.IsNullOrWhiteSpace(parameterName))
        {
            return $"parameter:{parameterName}";
        }

        if (!resource.TryGetLastAnnotation<DacpacTargetAnnotation>(out var target))
        {
            return $"step:{step.Name}";
        }

        parameterName = target.PipelineConnectionStringParameter?.Name;
        if (!string.IsNullOrWhiteSpace(parameterName))
        {
            return $"parameter:{parameterName}";
        }

        return $"reference:{target.Target.Name}:{target.TargetDatabaseName}";
    }

    private static void DependOn(PipelineStep step, string dependency)
    {
        if (!step.DependsOnSteps.Contains(dependency, StringComparer.Ordinal))
        {
            step.DependsOn(dependency);
        }
    }

    internal static IEnumerable<PipelineStep> CreateSteps(
        IResourceWithDacpac resource,
        DacpacPipelineEffectiveOptions options)
    {
        var source = GetSource(resource, options);
        var effectiveOptions = options with { Source = source };
        var build = resource.TryGetLastAnnotation<IProjectMetadata>(out _)
            ? CreateBuildStep(resource, effectiveOptions)
            : null;

        var publish = CreatePublishStep(resource, effectiveOptions);
        var report = CreateSqlPackageStep(
            resource,
            effectiveOptions,
            SqlPackageAction.DeployReport,
            $"report-{resource.Name}",
            "Generates a DACPAC deployment report.",
            "report.xml");
        var script = CreateSqlPackageStep(
            resource,
            effectiveOptions,
            SqlPackageAction.Script,
            $"script-{resource.Name}",
            "Generates a DACPAC deployment script.",
            "script.sql");
        var deploy = CreateSqlPackageStep(
            resource,
            effectiveOptions,
            SqlPackageAction.Publish,
            $"deploy-{resource.Name}",
            "Deploys the DACPAC.",
            outputFileName: null);

        foreach (var consumer in new[] { publish, report, script, deploy })
        {
            if (source == DacpacSource.Build)
            {
                consumer.DependsOn(build!);
            }
            else if (source == DacpacSource.File && consumer == publish)
            {
                consumer.DependsOn(WellKnownPipelineSteps.ProcessParameters);
            }
        }

        report.DependsOn(WellKnownPipelineSteps.ProcessParameters);
        script.DependsOn(WellKnownPipelineSteps.ProcessParameters);
        deploy.DependsOn(WellKnownPipelineSteps.ProcessParameters);

        if (options.IncludeInPublish)
        {
            publish.RequiredBy(WellKnownPipelineSteps.Publish);
        }

        if (options.IncludeInDeploy)
        {
            deploy.RequiredBy(WellKnownPipelineSteps.Deploy);
        }

        return build is null
            ? [publish, report, script, deploy]
            : [build, publish, report, script, deploy];
    }

    private static DacpacSource? GetSource(
        IResourceWithDacpac resource,
        DacpacPipelineEffectiveOptions options)
    {
        if (IsPackageResource(resource))
        {
            // Checked again here because package annotations are added after AddSqlProject registers the defaults.
            RejectPackageSourceOverride(resource, options.Source);
            return null;
        }

        if (options.Source is not null)
        {
            if (options.Source == DacpacSource.Build
                && !resource.TryGetLastAnnotation<IProjectMetadata>(out _))
            {
                throw new DistributedApplicationException(
                    $"DACPAC pipeline resource '{resource.Name}' cannot use source Build because it is not backed by a SQL project path.");
            }

            return options.Source;
        }

        // The include flags only choose the aggregate commands; whether to build comes from Source alone.
        return resource.TryGetLastAnnotation<IProjectMetadata>(out _)
            ? DacpacSource.Build
            : null;
    }

    // NuGet-backed resources always resolve their DACPAC from the package.
    internal static bool IsPackageResource(IResourceWithDacpac resource)
        => resource.TryGetLastAnnotation<SqlPackageRestoreAnnotation>(out _)
            || resource.TryGetLastAnnotation<IPackageMetadata>(out _);

    internal static void RejectPackageSourceOverride(IResourceWithDacpac resource, DacpacSource? source)
    {
        if (source is not null)
        {
            throw new DistributedApplicationException(
                $"DACPAC source overrides are not supported for package resource '{resource.Name}'.");
        }
    }

    private static PipelineStep CreateBuildStep(
        IResourceWithDacpac resource,
        DacpacPipelineEffectiveOptions options)
        => new()
        {
            Name = $"build-{resource.Name}",
            Description = "Builds the SQL project DACPAC.",
            Resource = resource,
            Action = async context =>
            {
                var resolver = context.Services.GetRequiredService<SqlProjectDacpacResolver>();
                var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
                using var dacpac = await resolver.ResolveAsync(
                    resource,
                    options.BuildConfiguration,
                    context.Logger,
                    context.CancellationToken);
                DacpacPipelineArtifacts.Copy(dacpac.Path, GetBuildOutputPath(resource, outputService));
            }
        };

    private static PipelineStep CreatePublishStep(
        IResourceWithDacpac resource,
        DacpacPipelineEffectiveOptions options)
        => new()
        {
            Name = $"publish-{resource.Name}",
            Description = "Publishes the DACPAC artifact.",
            Resource = resource,
            Action = async context =>
            {
                var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
                var source = await DacpacPipelineArtifacts.GetConsumerPathAsync(
                    resource,
                    options,
                    context,
                    GetBuildOutputPath(resource, outputService));
                var outputPath = Path.Combine(outputService.GetOutputDirectory(), resource.Name, $"{resource.Name}.dacpac");
                DacpacPipelineArtifacts.Copy(source, outputPath);
            }
        };

    private static PipelineStep CreateSqlPackageStep(
        IResourceWithDacpac resource,
        DacpacPipelineEffectiveOptions options,
        SqlPackageAction action,
        string name,
        string description,
        string? outputFileName)
        => new()
        {
            Name = name,
            Description = description,
            Resource = resource,
            Action = async context =>
            {
                var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
                var source = await DacpacPipelineArtifacts.GetConsumerPathAsync(
                    resource,
                    options,
                    context,
                    GetBuildOutputPath(resource, outputService));
                var target = GetTarget(resource, context.Model, options);
                var profilePath = GetProfilePath(resource, context.Services);
                var outputRoot = Path.IsPathRooted(options.OutputDirectory)
                    ? options.OutputDirectory
                    : Path.Combine(outputService.GetOutputDirectory(), options.OutputDirectory);
                var outputPath = outputFileName is null
                    ? null
                    : Path.Combine(outputRoot, resource.Name, outputFileName);
                var runner = context.Services.GetRequiredService<ISqlPackageRunner>();

                var connectionString = await target.ConnectionString.GetValueAsync(context.CancellationToken)
                    ?? throw new DistributedApplicationException(
                        $"The target connection string for resource '{resource.Name}' did not resolve to a value.");
                var targetLock = context.Services.GetRequiredService<SqlPackageTargetLock>();
                using (await targetLock.AcquireAsync(
                    connectionString,
                    target.DatabaseName,
                    context.CancellationToken))
                {
                    await runner.RunAsync(
                        new(
                            action,
                            options.SqlPackageVersion,
                            source,
                            outputPath,
                            connectionString,
                            target.DatabaseName,
                            profilePath),
                        context.Logger,
                        context.CancellationToken);
                }

                if (outputPath is not null && !File.Exists(outputPath))
                {
                    throw new DistributedApplicationException(
                        $"SqlPackage action '{action}' did not create the expected artifact '{outputPath}'.");
                }
            }
        };

    internal static (
        IValueProvider ConnectionString,
        string? DatabaseName) GetTarget(
        IResourceWithDacpac resource,
        DistributedApplicationModel model,
        DacpacPipelineEffectiveOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.TargetConnectionStringParameterName))
        {
            var parameter = FindSecretTargetParameter(model, options.TargetConnectionStringParameterName);
            if (parameter is null)
            {
                throw new DistributedApplicationException(
                    $"DACPAC target parameter '{options.TargetConnectionStringParameterName}' for resource '{resource.Name}' was not found in the application model.");
            }

            return (parameter, null);
        }

        if (!resource.TryGetLastAnnotation<DacpacTargetAnnotation>(out var target))
        {
            throw new DistributedApplicationException(
                $"DACPAC pipeline resource '{resource.Name}' has no target. Call WithReference before running report, script, or deploy operations.");
        }

        if (target.PipelineConnectionStringParameter is not null)
        {
            return (target.PipelineConnectionStringParameter, target.TargetDatabaseName);
        }

        return (target.Target.ConnectionStringExpression, target.TargetDatabaseName);
    }

    internal static void ValidateSecretTargetParameter(DistributedApplicationModel model, string? parameterName)
    {
        if (parameterName is not null)
        {
            _ = FindSecretTargetParameter(model, parameterName);
        }
    }

    internal static ParameterResource? FindSecretTargetParameter(DistributedApplicationModel model, string name)
    {
        var parameter = model.Resources.TryGetByName(name, out var resource) ? resource as ParameterResource : null;
        if (parameter is { Secret: false })
        {
            throw new DistributedApplicationException(
                $"DACPAC target parameter '{name}' must be a secret parameter. Create it with AddParameter(..., secret: true).");
        }

        return parameter;
    }

    internal static string? GetProfilePath(
        IResourceWithDacpac resource,
        IServiceProvider services)
    {
        if (resource.HasAnnotationOfType<ConfigureDacDeployOptionsAnnotation>())
        {
            throw new DistributedApplicationException(
                $"DACPAC pipeline resource '{resource.Name}' uses WithConfigureDacDeployOptions, which cannot be serialized to SqlPackage. Use WithDacDeployOptions with a publish profile instead.");
        }

        if (!resource.TryGetLastAnnotation<DacDeployOptionsAnnotation>(out var options))
        {
            return null;
        }

        var environment = services.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        return Path.IsPathRooted(options.OptionsPath)
            ? options.OptionsPath
            : Path.GetFullPath(options.OptionsPath, environment.ContentRootPath);
    }

    private static string GetBuildOutputPath(
        IResourceWithDacpac resource,
        IPipelineOutputService outputService)
        => Path.Combine(outputService.GetOutputDirectory(), "build", resource.Name, $"{resource.Name}.dacpac");
}

#pragma warning restore ASPIREPIPELINES001

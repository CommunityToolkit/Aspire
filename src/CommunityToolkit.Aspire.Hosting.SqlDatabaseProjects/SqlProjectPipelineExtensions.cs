#pragma warning disable ASPIREPIPELINES001 // Pipeline APIs are experimental
#pragma warning disable ASPIREATS001 // Aspire export APIs are experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    internal static IResourceBuilder<TResource> RegisterPipeline<TResource>(
        IResourceBuilder<TResource> builder)
        where TResource : IResourceWithDacpac
        => builder.ApplicationBuilder.ExecutionContext.IsRunMode
            ? builder
            : InternalWithPipelineOptions(builder, null);

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
            : DacpacPipelineEffectiveOptions.CreateDefault(builder.Resource);
        var merged = DacpacPipelineConfiguration.Merge(builder, previous, options);

        builder.WithAnnotation(
            new DacpacPipelineOptionsAnnotation(merged),
            ResourceAnnotationMutationBehavior.Replace);

        if (merged.Source == DacpacSource.File
            && !builder.Resource.HasAnnotationOfType<DacpacPipelinePathParameterAnnotation>())
        {
            var parameterName = $"{builder.Resource.Name}-dacpac-path";
            var parameter = builder.ApplicationBuilder.AddParameter(parameterName)
                .WithDescription($"Path to the DACPAC used by the pipeline operations for '{builder.Resource.Name}'.");
            builder.WithAnnotation(new DacpacPipelinePathParameterAnnotation(parameter.Resource));
        }

        builder.ApplicationBuilder.Services.TryAddSingleton<SqlProjectDacpacResolver>();
        builder.ApplicationBuilder.Services.TryAddSingleton<ISqlPackageRunner, SqlPackageRunner>();

        if (!builder.Resource.HasAnnotationOfType<DacpacPipelineRegistrationAnnotation>())
        {
            var registerGlobalConfiguration = !builder.ApplicationBuilder.Resources
                .OfType<IResourceWithDacpac>()
                .Any(resource =>
                    !ReferenceEquals(resource, builder.Resource)
                    && resource.HasAnnotationOfType<DacpacPipelineRegistrationAnnotation>());
            builder.WithAnnotation(new DacpacPipelineRegistrationAnnotation());
            if (registerGlobalConfiguration)
            {
                builder.WithPipelineConfiguration(SqlProjectPipelineSteps.ConfigureDeployDependencies);
            }

            builder.WithAnnotation(new PipelineStepAnnotation(_ =>
            {
                if (!builder.Resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var current))
                {
                    throw new DistributedApplicationException(
                        $"DACPAC pipeline options for resource '{builder.Resource.Name}' are unavailable.");
                }

                return SqlProjectPipelineSteps.CreateSteps(builder.Resource, current.Options);
            }));
        }

        return builder;
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
    public static DacpacPipelineEffectiveOptions CreateDefault(IResourceWithDacpac resource)
        => new(
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
    internal static DacpacPipelineEffectiveOptions Merge<TResource>(
        IResourceBuilder<TResource> builder,
        DacpacPipelineEffectiveOptions previous,
        DacpacPipelineOptions? options)
        where TResource : IResourceWithDacpac
    {
        var source = options?.Source ?? previous.Source;
        var buildConfiguration = Normalize(options?.BuildConfiguration, previous.BuildConfiguration, "BuildConfiguration");
        var outputDirectory = Normalize(options?.OutputDirectory, previous.OutputDirectory, "OutputDirectory");
        var sqlPackageVersion = Normalize(options?.SqlPackageVersion, previous.SqlPackageVersion, "SqlPackageVersion");
        var targetParameter = NormalizeOptional(
            options?.TargetConnectionStringParameterName,
            previous.TargetConnectionStringParameterName,
            "TargetConnectionStringParameterName");

        var prefix = $"DacpacPipeline:{builder.Resource.Name}:";
        source = GetEnum(builder.ApplicationBuilder.Configuration[prefix + nameof(DacpacPipelineOptions.Source)], source);
        var includeInPublish = GetBoolean(
            builder.ApplicationBuilder.Configuration[prefix + nameof(DacpacPipelineOptions.IncludeInPublish)],
            options?.IncludeInPublish ?? previous.IncludeInPublish);
        var includeInDeploy = GetBoolean(
            builder.ApplicationBuilder.Configuration[prefix + nameof(DacpacPipelineOptions.IncludeInDeploy)],
            options?.IncludeInDeploy ?? previous.IncludeInDeploy);
        buildConfiguration = Normalize(
            builder.ApplicationBuilder.Configuration[prefix + nameof(DacpacPipelineOptions.BuildConfiguration)],
            buildConfiguration,
            nameof(DacpacPipelineOptions.BuildConfiguration));
        outputDirectory = Normalize(
            builder.ApplicationBuilder.Configuration[prefix + nameof(DacpacPipelineOptions.OutputDirectory)],
            outputDirectory,
            nameof(DacpacPipelineOptions.OutputDirectory));
        sqlPackageVersion = Normalize(
            builder.ApplicationBuilder.Configuration[prefix + nameof(DacpacPipelineOptions.SqlPackageVersion)],
            sqlPackageVersion,
            nameof(DacpacPipelineOptions.SqlPackageVersion));
        targetParameter = NormalizeOptional(
            builder.ApplicationBuilder.Configuration[prefix + nameof(DacpacPipelineOptions.TargetConnectionStringParameterName)],
            targetParameter,
            nameof(DacpacPipelineOptions.TargetConnectionStringParameterName));

        ValidateSource(builder.Resource, source);

        return new(
            includeInPublish,
            includeInDeploy,
            source,
            buildConfiguration,
            outputDirectory,
            sqlPackageVersion,
            targetParameter);
    }

    private static void ValidateSource(IResourceWithDacpac resource, DacpacSource? source)
    {
        if (resource.TryGetLastAnnotation<SqlPackageRestoreAnnotation>(out _)
            || resource.TryGetLastAnnotation<IPackageMetadata>(out _))
        {
            if (source is not null)
            {
                throw new DistributedApplicationException(
                    $"DACPAC source overrides are not supported for package resource '{resource.Name}'.");
            }

            return;
        }

    }

    private static string Normalize(string? value, string fallback, string name)
    {
        if (value is null)
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DistributedApplicationException($"DACPAC pipeline option '{name}' cannot be empty.");
        }

        return value;
    }

    private static string? NormalizeOptional(string? value, string? fallback, string name)
    {
        if (value is null)
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DistributedApplicationException($"DACPAC pipeline option '{name}' cannot be empty.");
        }

        return value;
    }

    private static bool GetBoolean(string? value, bool fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        if (!bool.TryParse(value, out var parsed))
        {
            throw new DistributedApplicationException($"DACPAC pipeline Boolean value '{value}' is invalid.");
        }

        return parsed;
    }

    private static DacpacSource? GetEnum(string? value, DacpacSource? fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        if (!Enum.TryParse<DacpacSource>(value, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new DistributedApplicationException($"DACPAC pipeline source '{value}' is invalid.");
        }

        return parsed;
    }
}

/// <summary>
/// Creates and configures DACPAC pipeline steps.
/// </summary>
internal static class SqlProjectPipelineSteps
{
    internal static void ConfigureDeployDependencies(PipelineConfigurationContext context)
        => ConfigureDeployDependencies(context.Steps);

    internal static void ConfigureDeployDependencies(IReadOnlyList<PipelineStep> steps)
    {
        var provisionSteps = steps
            .Where(step => step.Tags.Contains(WellKnownPipelineTags.ProvisionInfrastructure))
            .ToList();
        var deploySteps = steps
            .Where(step => step.Name.StartsWith("deploy-", StringComparison.Ordinal)
                && step.Resource is IResourceWithDacpac)
            .ToList();
        var aggregateRoots = deploySteps
            .Where(step =>
                step.Resource is IResourceWithDacpac resource
                && resource.TryGetLastAnnotation<DacpacPipelineOptionsAnnotation>(out var options)
                && options.Options.IncludeInDeploy)
            .ToList();
        var closure = GetDeployClosure(aggregateRoots, deploySteps);

        foreach (var step in closure)
        {
            var resource = (IResourceWithDacpac)step.Resource!;
            foreach (var provisionStep in provisionSteps)
            {
                DependOn(step, provisionStep.Name);
            }
            ConfigureProjectDependencies(step, resource, steps);
        }

        foreach (var group in GetTargetGroups(closure))
        {
            var ordered = TopologicalOrder(group, closure);
            for (var index = 1; index < ordered.Count; index++)
            {
                DependOn(ordered[index], ordered[index - 1].Name);
            }
        }
    }

    private static HashSet<PipelineStep> GetDeployClosure(
        IEnumerable<PipelineStep> roots,
        IReadOnlyList<PipelineStep> deploySteps)
    {
        var closure = new HashSet<PipelineStep>();
        var pending = new Stack<PipelineStep>(roots);

        while (pending.TryPop(out var step))
        {
            if (!closure.Add(step)
                || step.Resource is not IResourceWithDacpac resource
                || !resource.TryGetAnnotationsOfType<WaitAnnotation>(out var waits))
            {
                continue;
            }

            foreach (var dependency in waits
                         .Select(wait => wait.Resource)
                         .OfType<IResourceWithDacpac>())
            {
                var dependencyStep = deploySteps.FirstOrDefault(
                    candidate => candidate.Name == $"deploy-{dependency.Name}");
                if (dependencyStep is not null)
                {
                    pending.Push(dependencyStep);
                }
            }
        }

        return closure;
    }

    private static void ConfigureProjectDependencies(
        PipelineStep deploy,
        IResourceWithDacpac resource,
        IReadOnlyList<PipelineStep> steps)
    {
        if (!resource.TryGetAnnotationsOfType<WaitAnnotation>(out var waits))
        {
            return;
        }

        foreach (var wait in waits)
        {
            if (wait.Resource is not IResourceWithDacpac dependency)
            {
                continue;
            }

            var dependencyStep = steps.FirstOrDefault(
                step => step.Name == $"deploy-{dependency.Name}");
            if (dependencyStep is not null && dependencyStep != deploy)
            {
                DependOn(deploy, dependencyStep.Name);
            }
        }
    }

    private static IEnumerable<IReadOnlyList<PipelineStep>> GetTargetGroups(
        HashSet<PipelineStep> deploySteps)
    {
        var ungrouped = deploySteps.ToList();
        while (ungrouped.Count > 0)
        {
            var first = ungrouped[0];
            var group = ungrouped
                .Where(candidate => TargetsMatch(first, candidate))
                .ToList();
            foreach (var item in group)
            {
                ungrouped.Remove(item);
            }

            yield return group;
        }
    }

    private static List<PipelineStep> TopologicalOrder(
        IReadOnlyList<PipelineStep> group,
        HashSet<PipelineStep> closure)
    {
        var groupNames = group.Select(step => step.Name).ToHashSet(StringComparer.Ordinal);
        var dependencies = group.ToDictionary(
            step => step,
            step => GetTransitiveDependencies(step, closure)
                .Where(groupNames.Contains)
                .ToHashSet(StringComparer.Ordinal));
        var indegree = dependencies.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Count);

        var ready = new SortedSet<PipelineStep>(
            group.Where(step => indegree[step] == 0),
            Comparer<PipelineStep>.Create((left, right) =>
                StringComparer.Ordinal.Compare(left.Name, right.Name)));
        var ordered = new List<PipelineStep>(group.Count);
        while (ready.Count > 0)
        {
            var current = ready.Min!;
            ready.Remove(current);
            ordered.Add(current);

            foreach (var dependent in group.Where(step =>
                         dependencies[step].Contains(current.Name)))
            {
                indegree[dependent]--;
                if (indegree[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (ordered.Count != group.Count)
        {
            throw new DistributedApplicationException(
                $"DACPAC deployment dependencies for target '{GetTargetKey(group[0])}' contain a cycle.");
        }

        return ordered;
    }

    private static HashSet<string> GetTransitiveDependencies(
        PipelineStep step,
        HashSet<PipelineStep> steps)
    {
        var names = steps.ToDictionary(candidate => candidate.Name, StringComparer.Ordinal);
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(step.DependsOnSteps);
        while (pending.TryPop(out var dependency))
        {
            if (!dependencies.Add(dependency)
                || !names.TryGetValue(dependency, out var dependencyStep))
            {
                continue;
            }

            foreach (var nested in dependencyStep.DependsOnSteps)
            {
                pending.Push(nested);
            }
        }

        return dependencies;
    }

    private static bool TargetsMatch(PipelineStep left, PipelineStep right)
        => string.Equals(GetTargetKey(left), GetTargetKey(right), StringComparison.Ordinal);

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
        if (resource.TryGetLastAnnotation<SqlPackageRestoreAnnotation>(out _)
            || resource.TryGetLastAnnotation<IPackageMetadata>(out _))
        {
            if (options.Source is not null)
            {
                throw new DistributedApplicationException(
                    $"DACPAC source overrides are not supported for package resource '{resource.Name}'.");
            }

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

        return resource.TryGetLastAnnotation<IProjectMetadata>(out _)
            ? options.IncludeInPublish || options.IncludeInDeploy
                ? DacpacSource.Build
                : DacpacSource.Existing
            : null;
    }

    private static PipelineStep CreatePendingStep(IResourceWithDacpac resource, string name, string description)
        => new()
        {
            Name = name,
            Description = description,
            Resource = resource,
            Action = _ => throw new DistributedApplicationException(
                $"DACPAC pipeline step '{name}' is registered but its action has not been implemented.")
        };

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
                await DacpacPipelineArtifacts.CopyAsync(
                    dacpac.Path,
                    GetBuildOutputPath(resource, outputService),
                    context.CancellationToken);
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
                await DacpacPipelineArtifacts.CopyAsync(source, outputPath, context.CancellationToken);
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

                await runner.RunAsync(
                    new(
                        action,
                        options.SqlPackageVersion,
                        source,
                        outputPath,
                        await target.ConnectionString.GetValueAsync(context.CancellationToken)
                            ?? throw new DistributedApplicationException(
                                $"The target connection string for resource '{resource.Name}' did not resolve to a value."),
                        target.DatabaseName,
                        profilePath),
                    context.Logger,
                    context.CancellationToken);

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
            var parameter = model.Resources
                .OfType<ParameterResource>()
                .SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.Name,
                        options.TargetConnectionStringParameterName,
                        StringComparison.Ordinal));
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

/// <summary>
/// Resolves and copies DACPAC artifacts used by pipeline steps.
/// </summary>
internal static class DacpacPipelineArtifacts
{
    public static async Task<string> GetConsumerPathAsync(
        IResourceWithDacpac resource,
        DacpacPipelineEffectiveOptions options,
        PipelineStepContext context,
        string buildOutputPath)
    {
        if (options.Source == DacpacSource.Build)
        {
            ValidateFile(resource, buildOutputPath);
            return buildOutputPath;
        }

        if (options.Source == DacpacSource.File)
        {
            if (!resource.TryGetLastAnnotation<DacpacPipelinePathParameterAnnotation>(out var parameter))
            {
                throw new DistributedApplicationException(
                    $"DACPAC path parameter for resource '{resource.Name}' is unavailable.");
            }

            var value = await parameter.Parameter.GetValueAsync(context.CancellationToken);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new DistributedApplicationException(
                    $"Parameter '{parameter.Parameter.Name}' does not specify a DACPAC path for resource '{resource.Name}'.");
            }

            var environment = context.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
            var path = Path.IsPathRooted(value)
                ? value
                : Path.GetFullPath(value, environment.ContentRootPath);
            ValidateFile(resource, path);
            return path;
        }

        var resolver = context.Services.GetRequiredService<SqlProjectDacpacResolver>();
        using var dacpac = options.Source == DacpacSource.Existing
            ? await resolver.ResolveExistingAsync(resource, context.Logger, context.CancellationToken)
            : await resolver.ResolveAsync(resource, context.Logger, context.CancellationToken);
        if (!File.Exists(dacpac.Path))
        {
            throw new DistributedApplicationException(
                $"DACPAC for resource '{resource.Name}' was not found at '{dacpac.Path}'.");
        }

        return dacpac.Path;
    }

    public static async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        if (!File.Exists(source))
        {
            throw new DistributedApplicationException($"DACPAC '{source}' was not found.");
        }

        if (string.Equals(source, destination, GetPathComparison()))
        {
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var input = new FileStream(
                source,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destination, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static void ValidateFile(IResourceWithDacpac resource, string path)
    {
        if (!File.Exists(path))
        {
            throw new DistributedApplicationException(
                $"DACPAC for resource '{resource.Name}' was not found at '{path}'.");
        }
    }

    private static StringComparison GetPathComparison()
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

#pragma warning restore ASPIREPIPELINES001
#pragma warning restore ASPIREATS001

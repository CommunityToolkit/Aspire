#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES003
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPROBES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Coordinates the Aspire publish, deploy, and destroy pipeline steps for Vercel while
/// delegating provider formats and app-model rules to focused testable helpers.
/// </summary>
internal static partial class VercelDeploymentStep
{
    // E2E contract:
    //   publish => deterministic, reviewable vercel-deployments.json with no provider mutation
    //   deploy prereq => validate tools/auth, create/link projects, configure VCR registry annotations
    //   built-in Aspire build/push => build the actual workload image and push it to VCR
    //   deploy => resolve the pushed tag to a linux/amd64 digest, write Build Output API metadata,
    //             run `vercel deploy --prebuilt`, verify with `vercel inspect`, then save state
    //   destroy => use saved state, not the current model, and delete only Aspire-managed projects.
    // Keep provider/protocol parsing in small internal helpers so tests can assert exact behavior
    // without live Vercel credentials.
    //
    // Vercel contracts referenced by the non-obvious deploy behavior below:
    //   Container Images: https://vercel.com/docs/functions/container-images
    //   Container Registry: https://vercel.com/docs/container-registry
    //   Build Output API: https://vercel.com/docs/build-output-api
    //   Prebuilt deploy: https://vercel.com/docs/cli/deploy
    public const string PublishStepNamePrefix = "vercel-generate-plan-";
    public const string DeployPrereqStepNamePrefix = "vercel-prepare-projects-";
    public const string DeployStepNamePrefix = "vercel-deploy-prebuilt-";
    public const string DestroyPrereqStepNamePrefix = "vercel-prepare-destroy-";
    public const string DestroyStepNamePrefix = "vercel-destroy-";

    private const string VercelCliFileName = VercelConstants.CliFileName;
    internal const string DockerCliFileName = VercelConstants.DockerCliFileName;
    private const string VcrRegistry = VercelConstants.VcrRegistry;
    private const string VercelContainerServiceName = VercelConstants.ContainerServiceName;
    private const string PushPrereqStepName = "push-prereq";
    private static readonly Version MinimumVercelCliVersion = new(54, 18, 6);

    public static async Task ValidatePrerequisitesAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        // This is the deploy prerequisite step, not merely static validation. It intentionally
        // performs the provider reads/mutations required before Aspire's shared push-prereq
        // step can validate registry metadata: project create/link, Vercel pull, VCR login,
        // VCR repository ensure, and deployment target annotation setup.
        var options = environment.GetVercelOptions();
        var entries = VercelDeploymentModel.GetEntries(context.Model, environment).ToList();
        VercelDeploymentModel.ValidateEntries(entries);
        foreach (var entry in entries)
        {
            await VercelDeploymentModel.ValidateVercelJsonAsync(entry.Resource, entry.SourceRoot, context.CancellationToken).ConfigureAwait(false);
        }

        var projectMap = await VercelDeploymentProjectGrouper.CreateMapAsync(context.ExecutionContext, context.Logger, entries, context.CancellationToken).ConfigureAwait(false);

        await ValidateCliPrerequisitesAsync(context, environment).ConfigureAwait(false);
        await ValidateDockerDigestInspectionPrerequisitesAsync(context).ConfigureAwait(false);
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var registryClient = context.Services.GetRequiredService<IVercelContainerRegistryClient>();
        await VercelDeploymentStateStore.ValidateExistingAsync(context, environment, options).ConfigureAwait(false);
        var entriesByResourceName = VercelDeploymentModel.GetEntriesByResourceName(entries);

        foreach (var group in projectMap.Groups)
        {
            await PrepareProjectGroupForBuiltInImagePushAsync(
                context,
                environment,
                options,
                runner,
                registryClient,
                entriesByResourceName,
                projectMap,
                group).ConfigureAwait(false);
        }
    }

    private static async Task ValidateDockerDigestInspectionPrerequisitesAsync(PipelineStepContext context)
    {
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var result = await runner.ValidateDockerBuildxAsync(context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException("validate Docker buildx for VCR image digest inspection", DockerCliFileName, result);
        }
    }

    public static async Task DeployAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var entries = VercelDeploymentModel.GetEntries(context.Model, environment).ToList();

        VercelDeploymentModel.ValidateEntries(entries);
        var projectMap = await VercelDeploymentProjectGrouper.CreateMapAsync(context.ExecutionContext, context.Logger, entries, context.CancellationToken).ConfigureAwait(false);
        // Normal pipeline execution runs the prereq step first. Keep this fallback so the
        // deploy delegate remains directly unit-testable and safe if invoked by a custom runner.
        if (entries.Any(entry => GetPreparedDeployment(entry.Resource) is null))
        {
            await ValidatePrerequisitesAsync(context, environment).ConfigureAwait(false);
        }

        List<(
            VercelDeploymentProjectGroup Group,
            VercelPreparedDeploymentAnnotation RootDeployment,
            IReadOnlyList<VercelResolvedDeployment> ResolvedDeployments)> preparedGroups = [];

        foreach (var group in projectMap.Groups)
        {
            List<VercelResolvedDeployment> resolvedDeployments = [];
            foreach (var service in group.Services)
            {
                var preparedDeployment = GetPreparedDeployment(service.Entry.Resource);
                if (preparedDeployment is null)
                {
                    throw new DistributedApplicationException($"Resource '{service.Entry.Resource.Name}' was not prepared for Vercel deployment. Run the '{DeployPrereqStepNamePrefix}{environment.Name}' pipeline step before deploying.");
                }

                // Docker auth for VCR is registry-host scoped, so keep the re-login/digest inspect
                // phase sequential before producing the single Vercel project deployment.
                var image = await ResolvePushedImageDigestAsync(context, runner, preparedDeployment).ConfigureAwait(false);
                resolvedDeployments.Add(new(preparedDeployment, image));
            }

            var rootDeployment = resolvedDeployments.Single(resolved => resolved.PreparedDeployment.Entry.Resource.Name == group.RootEntry.Resource.Name).PreparedDeployment;
            VercelDeploymentService[] preparedServices = [.. group.Services
                .Select(service =>
                {
                    var preparedDeployment = resolvedDeployments.Single(resolved => resolved.PreparedDeployment.Entry.Resource.Name == service.Entry.Resource.Name).PreparedDeployment;
                    return service with
                    {
                        Entry = preparedDeployment.Entry,
                        ServiceName = preparedDeployment.ServiceName
                    };
                })];
            var preparedGroup = group with
            {
                Root = preparedServices.Single(service => string.Equals(service.Entry.Resource.Name, group.RootEntry.Resource.Name, StringComparison.Ordinal)),
                Services = preparedServices
            };

            preparedGroups.Add((preparedGroup, rootDeployment, resolvedDeployments));
        }

        var deploymentOutcomes = await Task.WhenAll(preparedGroups.Select((preparedGroup, index) => DeployProjectGroupAsync(preparedGroup, index))).ConfigureAwait(false);
        foreach (var outcome in deploymentOutcomes.OrderBy(static outcome => outcome.Index))
        {
            if (outcome.Exception is not null || outcome.DeploymentResult is null)
            {
                continue;
            }

            string? productionUrl = VercelDeploymentStateStore.GetProductionUrl(options, outcome.RootDeployment.ProjectLink.ProjectName);
            var stateEntry = CreateSuccessfulDeploymentStateEntry(
                outcome.Group,
                outcome.RootDeployment.ProjectLink,
                outcome.RootDeployment.ProjectContext,
                outcome.DeploymentResult,
                outcome.ResolvedDeployments,
                outcome.RootDeployment.ManagedByAspire,
                productionUrl);

            // Persist each verified project group independently. Another group can fail after
            // Vercel has already created earlier projects, and destroy must still know which
            // managed projects are safe to remove or retry.
            await VercelDeploymentStateStore.SaveEntryAsync(context, environment, options, stateEntry).ConfigureAwait(false);

            AddDeploymentSummary(context, outcome.Group.RootEntry.Resource.Name, outcome.DeploymentResult, productionUrl);
        }

        var failure = deploymentOutcomes.FirstOrDefault(static outcome => outcome.Exception is not null);
        if (failure.Exception is not null)
        {
            ExceptionDispatchInfo.Capture(failure.Exception).Throw();
        }

        async Task<(
            int Index,
            VercelDeploymentProjectGroup Group,
            VercelPreparedDeploymentAnnotation RootDeployment,
            IReadOnlyList<VercelResolvedDeployment> ResolvedDeployments,
            VercelDeploymentResult? DeploymentResult,
            Exception? Exception)> DeployProjectGroupAsync(
                (VercelDeploymentProjectGroup Group, VercelPreparedDeploymentAnnotation RootDeployment, IReadOnlyList<VercelResolvedDeployment> ResolvedDeployments) preparedGroup,
                int index)
        {
            try
            {
                var deploymentResult = await DeployPrebuiltOutputAsync(
                    context,
                    runner,
                    options,
                    preparedGroup.Group,
                    preparedGroup.RootDeployment,
                    preparedGroup.ResolvedDeployments).ConfigureAwait(false);
                return (index, preparedGroup.Group, preparedGroup.RootDeployment, preparedGroup.ResolvedDeployments, deploymentResult, Exception: null);
            }
            catch (Exception ex)
            {
                return (index, preparedGroup.Group, preparedGroup.RootDeployment, preparedGroup.ResolvedDeployments, DeploymentResult: null, ex);
            }
        }
    }

    private static async Task EnsureManagedProjectAndSaveInitialStateAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        IVercelCliRunner runner,
        VercelDeploymentEntry preparedEntry,
        string sourceRoot,
        VercelProjectLink projectLink,
        PreviousVercelDeployment? previousDeployment)
    {
        // Create/validate the project before env configuration and VCR auth. Deploy can
        // take --project; env configuration and `vercel pull` link only a scratch directory
        // because the project-scoped env command has no --project option.
        await EnsureManagedProjectAsync(context, runner, options, preparedEntry).ConfigureAwait(false);

        // Record the managed project as soon as Vercel accepts it. Build, env
        // configuration, or deploy can still fail afterward, and destroy must be able
        // to clean up that partially-created provider object.
        await VercelDeploymentStateStore.SaveEntryAsync(context, environment, options, new(
            preparedEntry.Resource.Name,
            projectLink.ProjectName,
            projectLink.ProjectId,
            DeploymentId: null,
            DeploymentUrl: null,
            sourceRoot,
            ManagedByAspire: true)
        {
            ProductionUrl = VercelDeploymentStateStore.GetProductionUrl(options, projectLink.ProjectName),
            ProjectEnvironmentVariables = previousDeployment?.Entry.ProjectEnvironmentVariables ?? []
        }).ConfigureAwait(false);
    }

    private static void ValidateProjectEnvironmentVariableCollisions(
        VercelDeploymentProjectGroup group,
        IReadOnlyDictionary<string, VercelEnvironmentConfiguration> environmentConfigurations)
    {
        var collisions = group.Services
            .SelectMany(service => environmentConfigurations[service.Entry.Resource.Name].ProjectEnvironmentVariables
                .Select(variable => new
                {
                    variable.Key,
                    ResourceName = service.Entry.Resource.Name
                }))
            .GroupBy(static item => item.Key, StringComparer.Ordinal)
            .Where(static grouping => grouping.Select(static item => item.ResourceName).Distinct(StringComparer.Ordinal).Count() > 1)
            .ToArray();

        if (collisions.Length == 0)
        {
            return;
        }

        var collision = collisions[0];
        string resources = string.Join(", ", collision.Select(static item => $"'{item.ResourceName}'").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        throw new DistributedApplicationException($"Multiple services in Vercel project root '{group.RootEntry.Resource.Name}' configure secret project environment variable '{collision.Key}' ({resources}). Vercel project environment variables are project-scoped; use distinct environment variable names or move the value to a non-secret per-service setting.");
    }

    public static void ConfigurePipeline(PipelineConfigurationContext context, VercelEnvironmentResource environment)
    {
        // Pipeline ordering is the main integration point with Aspire's built-in container
        // support. Vercel prereq must happen before push-prereq/build/push so the resource has
        // a VCR container registry annotation and linux/amd64 build target; Vercel deploy must
        // then wait for the resource-specific push step.
        var entries = VercelDeploymentModel.GetEntries(context.Model, environment).ToArray();
        if (entries.Length == 0)
        {
            return;
        }

        string planStepName = $"{PublishStepNamePrefix}{environment.Name}";
        string prereqStepName = $"{DeployPrereqStepNamePrefix}{environment.Name}";
        var deploySteps = context.GetSteps(environment, "vercel-deploy").ToArray();
        var pushPrereqSteps = context.Steps
            .Where(static step => string.Equals(step.Name, PushPrereqStepName, StringComparison.Ordinal))
            .ToArray();

        foreach (var entry in entries)
        {
            EnsureVercelImagePushOptionsCallback(entry.Resource);

            var buildSteps = context.GetSteps(entry.Resource, WellKnownPipelineTags.BuildCompute);
            foreach (var buildStep in buildSteps)
            {
                AddUnique(buildStep.DependsOnSteps, prereqStepName);
                AddUnique(buildStep.RequiredBySteps, WellKnownPipelineSteps.Deploy);
                RemoveDuplicates(buildStep.DependsOnSteps);
                RemoveDuplicates(buildStep.RequiredBySteps);
            }

            var pushSteps = context.GetSteps(entry.Resource, WellKnownPipelineTags.PushContainerImage).ToArray();
            foreach (var pushStep in pushSteps)
            {
                AddUnique(pushStep.DependsOnSteps, prereqStepName);
                AddUnique(pushStep.RequiredBySteps, WellKnownPipelineSteps.Deploy);
                RemoveDuplicates(pushStep.DependsOnSteps);
                RemoveDuplicates(pushStep.RequiredBySteps);
            }

            // Aspire's global push prerequisite validates that every pushed image has a
            // registry. Vercel discovers the project-owned VCR registry through `vercel pull`,
            // so that validation must run after the Vercel prereq has annotated the resources.
            foreach (var pushPrereqStep in pushPrereqSteps)
            {
                AddUnique(pushPrereqStep.DependsOnSteps, prereqStepName);
                RemoveDuplicates(pushPrereqStep.DependsOnSteps);
                RemoveDuplicates(pushPrereqStep.RequiredBySteps);
            }

            foreach (var deployStep in deploySteps)
            {
                AddUnique(deployStep.DependsOnSteps, planStepName);
                foreach (var pushStep in pushSteps)
                {
                    AddUnique(deployStep.DependsOnSteps, pushStep.Name);
                }

                RemoveDuplicates(deployStep.DependsOnSteps);
                RemoveDuplicates(deployStep.RequiredBySteps);
            }
        }

        NormalizePipelineDependencies(context);
    }

    public static void NormalizePipelineDependencies(PipelineConfigurationContext context)
    {
        foreach (var step in context.Steps)
        {
            RemoveDuplicates(step.DependsOnSteps);
            RemoveDuplicates(step.RequiredBySteps);
        }
    }

    private static async Task PrepareProjectGroupForBuiltInImagePushAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        IVercelCliRunner runner,
        IVercelContainerRegistryClient registryClient,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        VercelDeploymentProjectMap projectMap,
        VercelDeploymentProjectGroup group)
    {
        List<VercelDeploymentService> preparedServices = [];
        foreach (var service in group.Services)
        {
            preparedServices.Add(service with
            {
                Entry = await VercelDeploymentModel.PrepareEntryAsync(context, service.Entry).ConfigureAwait(false)
            });
        }

        group = group with
        {
            Root = preparedServices.Single(service => service.IsPublicRoot),
            Services = [.. preparedServices]
        };

        var preparedRoot = group.Root.Entry;
        var projectLink = VercelProjectNameResolver.GetProjectLink(preparedRoot);
        var previousDeployment = await VercelDeploymentStateStore.GetPreviousAsync(
            context,
            environment,
            options,
            preparedRoot.Resource.Name,
            projectLink.ProjectName).ConfigureAwait(false);
        // A checked-in .vercel/project.json means the user linked an existing provider project,
        // so deploy can target it but destroy must not claim ownership of it.
        bool managedByAspire = !VercelProjectNameResolver.HasProjectLinkFile(preparedRoot.SourceRoot);

        // Managed projects are recorded before the image build begins. If Docker build,
        // project-env configuration, or VCR push fails later, destroy/retry still has enough
        // state to clean up the provider project and any tracked project env vars.
        if (managedByAspire)
        {
            await EnsureManagedProjectAndSaveInitialStateAsync(
                context,
                environment,
                options,
                runner,
                preparedRoot,
                preparedRoot.SourceRoot,
                projectLink,
                previousDeployment).ConfigureAwait(false);
        }

        var (projectContext, environmentConfigurations) = await PreparePulledProjectContextAsync(
            context,
            runner,
            options,
            group,
            entriesByResourceName,
            projectMap,
            previousDeployment).ConfigureAwait(false);

        await LoginToVcrAsync(context, runner, projectContext.PulledProject.OidcToken, projectContext.OidcClaims).ConfigureAwait(false);
        foreach (var service in group.Services)
        {
            await registryClient.EnsureRepositoryAsync(projectContext.PulledProject.OidcToken, projectContext.OidcClaims, service.ServiceName, context.CancellationToken).ConfigureAwait(false);

            AddVcrDeploymentAnnotations(
                environment,
                service,
                projectLink,
                projectContext,
                environmentConfigurations[service.Entry.Resource.Name],
                managedByAspire);
        }
    }

    internal static async Task<(VercelPulledProjectContext ProjectContext, IReadOnlyDictionary<string, VercelEnvironmentConfiguration> EnvironmentConfigurations)> PreparePulledProjectContextAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentProjectGroup group,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        VercelDeploymentProjectMap projectMap,
        PreviousVercelDeployment? previousDeployment = null)
    {
        string projectLinkDirectory = await PrepareProjectEnvironmentDirectoryAsync(context, runner, options, group.RootEntry).ConfigureAwait(false);
        try
        {
            Dictionary<string, VercelEnvironmentConfiguration> environmentConfigurations = new(StringComparer.Ordinal);
            foreach (var service in group.Services)
            {
                // Use Aspire's unprocessed values so endpoint references and secrets keep their
                // graph meaning until Vercel-specific deployment translation happens.
                environmentConfigurations[service.Entry.Resource.Name] = await VercelDeploymentPlanWriter.GetEnvironmentConfigurationAsync(
                    context.ExecutionContext,
                    context.Logger,
                    options,
                    service.Entry,
                    entriesByResourceName,
                    projectMap,
                    resolveProjectEnvironmentVariableValues: true,
                    context.CancellationToken).ConfigureAwait(false);
            }

            ValidateProjectEnvironmentVariableCollisions(group, environmentConfigurations);
            var projectEnvironmentVariables = environmentConfigurations
                .SelectMany(static item => item.Value.ProjectEnvironmentVariables)
                .GroupBy(static variable => variable.Key, StringComparer.Ordinal)
                .Select(static group => group.First())
                .ToArray();

            // Vercel resolves project env vars during the provider-side build/runtime.
            // Configure secret-bearing values before deploy; non-secret per-deployment
            // values remain on `vercel deploy --env`.
            await ConfigureProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                group.RootEntry,
                projectLinkDirectory,
                projectEnvironmentVariables,
                previousDeployment).ConfigureAwait(false);

            // The VCR image is not enough for deployment. Vercel deploy consumes Build
            // Output API metadata tied to a project, and VCR auth is minted by `vercel pull`
            // for that linked project. Keep that link/pull in scratch space so the source
            // root never receives provider metadata or pulled env files.
            var pulledProject = await PullProjectSettingsAsync(context, runner, options, group.RootEntry, projectLinkDirectory).ConfigureAwait(false);
            var oidcClaims = VercelOidcToken.DecodeUnvalidatedClaims(pulledProject.OidcToken);

            return (new(pulledProject, oidcClaims), environmentConfigurations);
        }
        finally
        {
            // `vercel pull` can write provider-owned env/config files into the linked
            // directory. They may contain project secrets unrelated to the AppHost, so the
            // scratch link is never copied, logged, stored in state, or left on disk.
            if (Directory.Exists(projectLinkDirectory))
            {
                Directory.Delete(projectLinkDirectory, recursive: true);
            }
        }
    }

    private static async Task<VercelDeploymentResult> DeployPrebuiltOutputAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentProjectGroup group,
        VercelPreparedDeploymentAnnotation rootDeployment,
        IReadOnlyList<VercelResolvedDeployment> resolvedDeployments)
    {
        // The generated Build Output API tree is the provider-owned deploy artifact.
        // It points at the immutable VCR digest and is written to temp/output storage so
        // `vercel deploy --prebuilt` uploads metadata only, not a staged source tree.
        await VercelBuildOutputWriter.WriteAsync(group, rootDeployment.ProjectContext.PulledProject, resolvedDeployments, context.CancellationToken).ConfigureAwait(false);

        var result = await runner.DeployPrebuiltAsync(
            options,
            group.RootEntry.DeployDirectory,
            rootDeployment.ProjectLink.ProjectId ?? rootDeployment.ProjectLink.ProjectName,
            [],
            context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"deploy prebuilt project root '{group.RootEntry.Resource.Name}' to Vercel", VercelCliFileName, result);
        }

        var deploymentResult = VercelCliOutputParser.GetDeploymentResult(result.StandardOutput);
        await VerifyDeploymentAsync(context, runner, options, group.RootEntry.Resource.Name, deploymentResult).ConfigureAwait(false);

        return deploymentResult;
    }

    private static void AddVcrDeploymentAnnotations(
        VercelEnvironmentResource environment,
        VercelDeploymentService service,
        VercelProjectLink projectLink,
        VercelPulledProjectContext projectContext,
        VercelEnvironmentConfiguration environmentConfiguration,
        bool managedByAspire)
    {
        var entry = service.Entry;
        EnsureVercelImagePushOptionsCallback(entry.Resource);

        var claims = projectContext.OidcClaims;
        if (string.IsNullOrWhiteSpace(claims.Owner)
            || string.IsNullOrWhiteSpace(claims.Project))
        {
            throw new DistributedApplicationException("The Vercel OIDC token did not include the owner and project claims required to construct a VCR image reference.");
        }

        RemoveAnnotations<VercelPreparedDeploymentAnnotation>(entry.Resource);
        RemoveAnnotations<DeploymentTargetAnnotation>(
            entry.Resource,
            annotation => ReferenceEquals(annotation.DeploymentTarget, environment));

        string repository = $"{claims.Owner}/{claims.Project}";
        string tag = $"aspire-{Guid.NewGuid():N}";
        string taggedImageReference = $"{VcrRegistry}/{repository}/{service.ServiceName}:{tag}";
        var registry = new ContainerRegistryResource(
            $"{environment.Name}-{entry.Resource.Name}-vcr",
            ReferenceExpression.Create($"{VcrRegistry}"),
            ReferenceExpression.Create($"{repository}"));

        entry.Resource.Annotations.Add(new DeploymentTargetAnnotation(environment)
        {
            ComputeEnvironment = entry.Resource.GetComputeEnvironment() ?? environment,
            ContainerRegistry = registry
        });

        entry.Resource.Annotations.Add(new VercelPreparedDeploymentAnnotation(
            entry,
            service.ServiceName,
            projectLink,
            projectContext,
            environmentConfiguration,
            managedByAspire,
            service.ServiceName,
            tag,
            taggedImageReference));
    }

    private static void EnsureVercelImagePushOptionsCallback(IResource resource)
    {
        if (resource.Annotations.OfType<VercelImagePushOptionsCallbackAnnotation>().Any())
        {
            return;
        }

        resource.Annotations.Add(new VercelImagePushOptionsCallbackAnnotation());
        resource.Annotations.Add(new ContainerBuildOptionsCallbackAnnotation(context =>
        {
            // Vercel's container runtime requires linux/amd64. Force the shared Aspire
            // build path to produce that platform even when the AppHost runs on ARM hosts.
            context.TargetPlatform = ContainerTargetPlatform.LinuxAmd64;
        }));
        resource.Annotations.Add(new ContainerImagePushOptionsCallbackAnnotation(context =>
        {
            var preparedDeployment = GetPreparedDeployment(context.Resource);
            if (preparedDeployment is null)
            {
                throw new DistributedApplicationException($"Resource '{context.Resource.Name}' was not prepared with Vercel Container Registry image push settings.");
            }

            context.Options.RemoteImageName = preparedDeployment.RemoteImageName;
            context.Options.RemoteImageTag = preparedDeployment.RemoteImageTag;
        }));
    }

    private static VercelPreparedDeploymentAnnotation? GetPreparedDeployment(IResource resource)
        => resource.Annotations.OfType<VercelPreparedDeploymentAnnotation>().LastOrDefault();

    private static async Task<VercelImageReference> ResolvePushedImageDigestAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelPreparedDeploymentAnnotation preparedDeployment)
    {
        // Re-login before inspection for the same project-scoped VCR-token reason as push:
        // Docker credentials are keyed by vcr.vercel.com, while the OIDC token is scoped to
        // one Vercel project/repository.
        await LoginToVcrAsync(
            runner,
            preparedDeployment.ProjectContext.PulledProject.OidcToken,
            preparedDeployment.ProjectContext.OidcClaims,
            context.CancellationToken).ConfigureAwait(false);

        var result = await runner.InspectDockerImageDigestAsync(preparedDeployment.TaggedImageReference, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"resolve pushed VCR image digest for resource '{preparedDeployment.Entry.Resource.Name}'", DockerCliFileName, result);
        }

        string digest = VercelDockerImageDigestParser.GetDigest(result.StandardOutput);
        string digestReference = preparedDeployment.TaggedImageReference[..preparedDeployment.TaggedImageReference.LastIndexOf(':')] + $"@{digest}";
        return new(digestReference, digest);
    }

    private static VercelDeploymentStateEntry CreateSuccessfulDeploymentStateEntry(
        VercelDeploymentProjectGroup group,
        VercelProjectLink projectLink,
        VercelPulledProjectContext projectContext,
        VercelDeploymentResult deploymentResult,
        IReadOnlyList<VercelResolvedDeployment> resolvedDeployments,
        bool managedByAspire,
        string? productionUrl)
        // State stores ownership, provider IDs/URLs, image digest, and env var names only.
        // Never persist secret values or temp project-link files from `vercel pull`.
        => new(
            group.RootEntry.Resource.Name,
            projectLink.ProjectName,
            projectLink.ProjectId ?? projectContext.PulledProject.ProjectId,
            deploymentResult.DeploymentId,
            deploymentResult.DeploymentUrl,
            group.RootEntry.SourceRoot,
            managedByAspire)
        {
            ProductionUrl = productionUrl,
            VcrImageDigest = resolvedDeployments.Single(resolved => resolved.PreparedDeployment.Entry.Resource.Name == group.RootEntry.Resource.Name).Image.Digest,
            Services = [.. resolvedDeployments
                .Select(static resolved => new VercelServiceDeploymentStateEntry(
                    resolved.PreparedDeployment.Entry.Resource.Name,
                    resolved.PreparedDeployment.ServiceName,
                    resolved.Image.Digest))
                .OrderBy(static service => service.ServiceName, StringComparer.Ordinal)],
            BuildOutputApiVersion = VercelConstants.BuildOutputApiVersion,
            ProjectEnvironmentVariables = [.. resolvedDeployments
                .SelectMany(static resolved => resolved.PreparedDeployment.EnvironmentConfiguration.ProjectEnvironmentVariables)
                .Select(static variable => variable.Key)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)]
        };

    private static void AddDeploymentSummary(
        PipelineStepContext context,
        string resourceName,
        VercelDeploymentResult deploymentResult,
        string? productionUrl)
    {
        context.Summary.Add($"{resourceName} Vercel deployment", deploymentResult.DeploymentUrl);
        if (productionUrl is not null)
        {
            context.Summary.Add($"{resourceName} Vercel production URL", productionUrl);
        }
    }

    private static void AddUnique(ICollection<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.Ordinal))
        {
            values.Add(value);
        }
    }

    private static void RemoveDuplicates(ICollection<string> values)
    {
        string[] distinctValues = values.Distinct(StringComparer.Ordinal).ToArray();
        if (distinctValues.Length == values.Count)
        {
            return;
        }

        values.Clear();
        foreach (string value in distinctValues)
        {
            values.Add(value);
        }
    }

    private static void RemoveAnnotations<TAnnotation>(IResource resource, Func<TAnnotation, bool>? predicate = null)
        where TAnnotation : IResourceAnnotation
    {
        foreach (var annotation in resource.Annotations.OfType<TAnnotation>().Where(annotation => predicate?.Invoke(annotation) ?? true).ToArray())
        {
            resource.Annotations.Remove(annotation);
        }
    }
}

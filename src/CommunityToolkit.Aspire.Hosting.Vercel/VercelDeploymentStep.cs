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

        await ValidateCliPrerequisitesAsync(context, environment).ConfigureAwait(false);
        await ValidateDockerDigestInspectionPrerequisitesAsync(context).ConfigureAwait(false);
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var registryClient = context.Services.GetRequiredService<IVercelContainerRegistryClient>();
        await VercelDeploymentStateStore.ValidateExistingAsync(context, environment, options).ConfigureAwait(false);
        var entriesByResourceName = VercelDeploymentModel.GetEntriesByResourceName(entries);

        foreach (var entry in entries)
        {
            await PrepareResourceForBuiltInImagePushAsync(
                context,
                environment,
                options,
                runner,
                registryClient,
                entriesByResourceName,
                entry).ConfigureAwait(false);
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
        // Normal pipeline execution runs the prereq step first. Keep this fallback so the
        // deploy delegate remains directly unit-testable and safe if invoked by a custom runner.
        if (entries.Any(entry => GetPreparedDeployment(entry.Resource) is null))
        {
            await ValidatePrerequisitesAsync(context, environment).ConfigureAwait(false);
        }

        foreach (var entry in entries)
        {
            await DeployEntryAsync(
                context,
                environment,
                options,
                runner,
                entry).ConfigureAwait(false);
        }
    }


    private static async Task DeployEntryAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        IVercelCliRunner runner,
        VercelDeploymentEntry entry)
    {
        var preparedDeployment = GetPreparedDeployment(entry.Resource);
        if (preparedDeployment is null)
        {
            throw new DistributedApplicationException($"Resource '{entry.Resource.Name}' was not prepared for Vercel deployment. Run the '{DeployPrereqStepNamePrefix}{environment.Name}' pipeline step before deploying.");
        }

        // At this point Aspire's built-in build/push steps have pushed the VCR tag selected
        // in prereq. Vercel requires the immutable linux/amd64 manifest digest in the Build
        // Output API handler, so deploy resolves the tag after push instead of trusting a tag.
        var image = await ResolvePushedImageDigestAsync(context, runner, preparedDeployment).ConfigureAwait(false);

        var deploymentResult = await DeployPrebuiltOutputAsync(
            context,
            runner,
            options,
            preparedDeployment.Entry,
            preparedDeployment.ProjectContext,
            image).ConfigureAwait(false);

        string? productionUrl = VercelDeploymentStateStore.GetProductionUrl(options, preparedDeployment.ProjectLink.ProjectName);
        var stateEntry = CreateSuccessfulDeploymentStateEntry(
            preparedDeployment.Entry,
            preparedDeployment.ProjectLink,
            preparedDeployment.ProjectContext,
            deploymentResult,
            image,
            preparedDeployment.ManagedByAspire,
            productionUrl);

        // Persist each verified resource independently. A later resource can fail after
        // Vercel has already created earlier projects, and destroy must still know which
        // managed projects are safe to remove or retry.
        await VercelDeploymentStateStore.SaveEntryAsync(context, environment, options, stateEntry).ConfigureAwait(false);

        AddDeploymentSummary(context, entry.Resource.Name, deploymentResult, productionUrl);
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

    private static async Task PrepareResourceForBuiltInImagePushAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        IVercelCliRunner runner,
        IVercelContainerRegistryClient registryClient,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        VercelDeploymentEntry entry)
    {
        var preparedEntry = await VercelDeploymentModel.PrepareEntryAsync(context, entry).ConfigureAwait(false);
        var projectLink = VercelProjectNameResolver.GetProjectLink(preparedEntry);
        var previousDeployment = await VercelDeploymentStateStore.GetPreviousAsync(
            context,
            environment,
            options,
            preparedEntry.Resource.Name,
            projectLink.ProjectName).ConfigureAwait(false);
        // A checked-in .vercel/project.json means the user linked an existing provider project,
        // so deploy can target it but destroy must not claim ownership of it.
        bool managedByAspire = !VercelProjectNameResolver.HasProjectLinkFile(entry.SourceRoot);

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
                preparedEntry,
                entry.SourceRoot,
                projectLink,
                previousDeployment).ConfigureAwait(false);
        }

        var projectContext = await PreparePulledProjectContextAsync(
            context,
            runner,
            options,
            preparedEntry,
            entriesByResourceName,
            previousDeployment).ConfigureAwait(false);

        await LoginToVcrAsync(context, runner, projectContext.PulledProject.OidcToken, projectContext.OidcClaims).ConfigureAwait(false);
        await registryClient.EnsureRepositoryAsync(projectContext.PulledProject.OidcToken, projectContext.OidcClaims, VercelContainerServiceName, context.CancellationToken).ConfigureAwait(false);

        AddVcrDeploymentAnnotations(environment, preparedEntry, projectLink, projectContext, managedByAspire);
    }

    internal static async Task<VercelPulledProjectContext> PreparePulledProjectContextAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry preparedEntry,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        PreviousVercelDeployment? previousDeployment = null)
    {
        string projectLinkDirectory = await PrepareProjectEnvironmentDirectoryAsync(context, runner, options, preparedEntry).ConfigureAwait(false);
        try
        {
            // Use Aspire's unprocessed values so endpoint references and secrets keep their
            // graph meaning until Vercel-specific deployment translation happens.
            var environmentConfiguration = await VercelDeploymentPlanWriter.GetEnvironmentConfigurationAsync(
                context.ExecutionContext,
                context.Logger,
                options,
                preparedEntry,
                entriesByResourceName,
                resolveProjectEnvironmentVariableValues: true,
                context.CancellationToken).ConfigureAwait(false);

            // Vercel resolves project env vars during the provider-side build/runtime.
            // Configure secret-bearing values before deploy; non-secret per-deployment
            // values remain on `vercel deploy --env`.
            await ConfigureProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                preparedEntry,
                projectLinkDirectory,
                environmentConfiguration.ProjectEnvironmentVariables,
                previousDeployment).ConfigureAwait(false);

            // The VCR image is not enough for deployment. Vercel deploy consumes Build
            // Output API metadata tied to a project, and VCR auth is minted by `vercel pull`
            // for that linked project. Keep that link/pull in scratch space so the source
            // root never receives provider metadata or pulled env files.
            var pulledProject = await PullProjectSettingsAsync(context, runner, options, preparedEntry, projectLinkDirectory).ConfigureAwait(false);
            var oidcClaims = VercelOidcToken.DecodeUnvalidatedClaims(pulledProject.OidcToken);

            return new(environmentConfiguration, pulledProject, oidcClaims);
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
        VercelDeploymentEntry entry,
        VercelPulledProjectContext projectContext,
        VercelImageReference image)
    {
        // The generated Build Output API tree is the provider-owned deploy artifact.
        // It points at the immutable VCR digest and is written to temp/output storage so
        // `vercel deploy --prebuilt` uploads metadata only, not a staged source tree.
        await VercelBuildOutputWriter.WriteAsync(entry, projectContext.PulledProject, image.Reference, context.CancellationToken).ConfigureAwait(false);

        var result = await runner.DeployPrebuiltAsync(
            options,
            entry.DeployDirectory,
            VercelProjectNameResolver.GetProjectOption(entry),
            projectContext.EnvironmentConfiguration.DeploymentEnvironmentVariables,
            context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"deploy prebuilt resource '{entry.Resource.Name}' to Vercel", VercelCliFileName, result);
        }

        var deploymentResult = VercelCliOutputParser.GetDeploymentResult(result.StandardOutput);
        await VerifyDeploymentAsync(context, runner, options, entry.Resource.Name, deploymentResult).ConfigureAwait(false);

        return deploymentResult;
    }

    private static void AddVcrDeploymentAnnotations(
        VercelEnvironmentResource environment,
        VercelDeploymentEntry entry,
        VercelProjectLink projectLink,
        VercelPulledProjectContext projectContext,
        bool managedByAspire)
    {
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
        string taggedImageReference = $"{VcrRegistry}/{repository}/{VercelContainerServiceName}:{tag}";
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
            projectLink,
            projectContext,
            managedByAspire,
            VercelContainerServiceName,
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
        VercelDeploymentEntry entry,
        VercelProjectLink projectLink,
        VercelPulledProjectContext projectContext,
        VercelDeploymentResult deploymentResult,
        VercelImageReference image,
        bool managedByAspire,
        string? productionUrl)
        // State stores ownership, provider IDs/URLs, image digest, and env var names only.
        // Never persist secret values or temp project-link files from `vercel pull`.
        => new(
            entry.Resource.Name,
            projectLink.ProjectName,
            projectLink.ProjectId ?? projectContext.PulledProject.ProjectId,
            deploymentResult.DeploymentId,
            deploymentResult.DeploymentUrl,
            entry.SourceRoot,
            managedByAspire)
        {
            ProductionUrl = productionUrl,
            VcrImageDigest = image.Digest,
            BuildOutputApiVersion = VercelConstants.BuildOutputApiVersion,
            ProjectEnvironmentVariables = [.. projectContext.EnvironmentConfiguration.ProjectEnvironmentVariables
                .Select(static variable => variable.Key)
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

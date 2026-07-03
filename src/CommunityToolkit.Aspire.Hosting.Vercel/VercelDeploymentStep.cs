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
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelDeploymentStep
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
    public const string PublishStepNamePrefix = "vercel-publish-";
    public const string DeployPrereqStepNamePrefix = "vercel-deploy-prereq-";
    public const string DeployStepNamePrefix = "vercel-deploy-";
    public const string DestroyPrereqStepNamePrefix = "vercel-destroy-prereq-";
    public const string DestroyStepNamePrefix = "vercel-destroy-";
    public const string DeploymentPlanFileName = "vercel-deployments.json";

    private const string StateSectionNamePrefix = "communitytoolkit.vercel.";
    private const int DeploymentStateSchemaVersion = 1;
    private const int VercelProjectNameMaxLength = 100;
    private const string VercelCliFileName = "vercel";
    internal const string DockerCliFileName = "docker";
    private const string VcrRegistry = "vcr.vercel.com";
    private const string VercelJsonFileName = "vercel.json";
    private const string VercelProjectFileName = "project.json";
    private const string VercelDirectoryName = ".vercel";
    private const string VercelOutputDirectoryName = "output";
    private const string VercelOidcTokenEnvironmentVariable = "VERCEL_OIDC_TOKEN";
    private const string VercelContainerServiceName = "app";
    private const string PushPrereqStepName = "push-prereq";
    private const int VercelBuildOutputApiVersion = 3;
    private static readonly Version MinimumVercelCliVersion = new(54, 18, 6);
    // These keys either bypass the generated Build Output API contract or configure
    // routing/build/runtime/env behavior that Aspire must own so endpoint refs and
    // secret handling stay coherent.
    private static readonly string[] VercelJsonBuildOutputUnsupportedKeys =
    [
        "build",
        "builds",
        "buildCommand",
        "devCommand",
        "env",
        "experimentalServices",
        "experimentalServicesV2",
        "framework",
        "functions",
        "headers",
        "ignoreCommand",
        "installCommand",
        "outputDirectory",
        "redirects",
        "rewrites",
        "routes",
        "services"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task WriteDeploymentPlanAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string outputDirectory = outputService.GetOutputDirectory(environment);

        string planPath = await WriteDeploymentPlanAsync(
            context.ExecutionContext,
            context.Logger,
            context.Model,
            environment,
            outputDirectory,
            context.CancellationToken).ConfigureAwait(false);

        context.Summary.Add("Vercel deployment plan", planPath);
    }

    internal static async Task<string> WriteDeploymentPlanAsync(
        DistributedApplicationModel model,
        VercelEnvironmentResource environment,
        string outputDirectory,
        CancellationToken cancellationToken)
        => await WriteDeploymentPlanAsync(
            executionContext: null,
            logger: null,
            model,
            environment,
            outputDirectory,
            cancellationToken).ConfigureAwait(false);

    internal static async Task<string> WriteDeploymentPlanAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        DistributedApplicationModel model,
        VercelEnvironmentResource environment,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var entries = GetDeploymentEntries(model, environment).ToList();
        ValidateEntries(entries);

        // Publish is a reviewable handoff, not a dry-run deploy. Keep it deterministic:
        // show commands, Dockerfile paths, and env var names without resolving secrets or
        // depending on mutable Vercel state.
        Directory.CreateDirectory(outputDirectory);
        var options = environment.GetVercelOptions();

        var plan = new VercelDeploymentPlan(
            environment.Name,
            await CreateDeploymentPlanEntriesAsync(
                executionContext,
                logger,
                options,
                entries,
                cancellationToken).ConfigureAwait(false));

        string planPath = Path.Combine(outputDirectory, DeploymentPlanFileName);
        await using FileStream stream = File.Create(planPath);
        await JsonSerializer.SerializeAsync(stream, plan, JsonOptions, cancellationToken).ConfigureAwait(false);

        return planPath;
    }

    public static async Task ValidatePrerequisitesAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        // This is the deploy prerequisite step, not merely static validation. It intentionally
        // performs the provider reads/mutations required before Aspire's shared push-prereq
        // step can validate registry metadata: project create/link, Vercel pull, VCR login,
        // VCR repository ensure, and deployment target annotation setup.
        var options = environment.GetVercelOptions();
        var entries = GetDeploymentEntries(context.Model, environment).ToList();
        ValidateEntries(entries);
        foreach (var entry in entries)
        {
            await ValidateVercelJsonAsync(entry.Resource, entry.SourceRoot, context.CancellationToken).ConfigureAwait(false);
        }

        await ValidateCliPrerequisitesAsync(context, environment).ConfigureAwait(false);
        await ValidateDockerDigestInspectionPrerequisitesAsync(context).ConfigureAwait(false);
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var registryClient = context.Services.GetRequiredService<IVercelContainerRegistryClient>();
        await ValidateExistingDeploymentStateAsync(context, environment, options).ConfigureAwait(false);
        var entriesByResourceName = GetDeploymentEntriesByResourceName(entries);

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
        var result = await runner.RunAsync(DockerCliFileName, ["buildx", "version"], workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException("validate Docker buildx for VCR image digest inspection", DockerCliFileName, result);
        }
    }

    public static async Task DeployAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();
        var entries = GetDeploymentEntries(context.Model, environment).ToList();

        ValidateEntries(entries);
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

    public static async Task ValidateCliPrerequisitesAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();

        var versionResult = await runner.RunAsync(VercelCliFileName, ["--version"], workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!versionResult.Succeeded)
        {
            throw CreateCliException("validate Vercel CLI installation", VercelCliFileName, versionResult);
        }

        var versionOutput = $"{versionResult.StandardOutput}{Environment.NewLine}{versionResult.StandardError}";
        if (!TryGetVercelCliVersion(versionOutput, out var version))
        {
            throw new DistributedApplicationException(
                $"Failed to determine Vercel CLI version from '{GetTrimmedOutput(versionOutput)}'. Install Vercel CLI {MinimumVercelCliVersion} or later from https://vercel.com/docs/cli.");
        }

        // The preview relies on newer CLI behavior: project-scoped link/pull,
        // prebuilt deploys, deployment-scoped --env, JSON inspect output with
        // --wait/--timeout, and project removal.
        if (version < MinimumVercelCliVersion)
        {
            throw new DistributedApplicationException(
                $"Vercel CLI version '{version}' is not supported. Install Vercel CLI {MinimumVercelCliVersion} or later from https://vercel.com/docs/cli.");
        }

        var whoamiResult = await runner.RunAsync(VercelCliFileName, ["whoami"], workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!whoamiResult.Succeeded)
        {
            throw CreateCliException("validate Vercel authentication", VercelCliFileName, whoamiResult);
        }

        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            var scopeResult = await runner.RunAsync(VercelCliFileName, BuildValidateScopeArguments(options), workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
            if (!scopeResult.Succeeded)
            {
                throw CreateCliException($"validate Vercel scope '{options.Scope}'", VercelCliFileName, scopeResult);
            }
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

        string? productionUrl = GetProductionUrl(options, preparedDeployment.ProjectLink.ProjectName);
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
        await SaveDeploymentStateEntryAsync(context, environment, options, stateEntry).ConfigureAwait(false);

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
        await SaveDeploymentStateEntryAsync(context, environment, options, new(
            preparedEntry.Resource.Name,
            projectLink.ProjectName,
            projectLink.ProjectId,
            DeploymentId: null,
            DeploymentUrl: null,
            sourceRoot,
            ManagedByAspire: true)
        {
            ProductionUrl = GetProductionUrl(options, projectLink.ProjectName),
            ProjectEnvironmentVariables = previousDeployment?.Entry.ProjectEnvironmentVariables ?? []
        }).ConfigureAwait(false);
    }

    public static void ConfigurePipeline(PipelineConfigurationContext context, VercelEnvironmentResource environment)
    {
        // Pipeline ordering is the main integration point with Aspire's built-in container
        // support. Vercel prereq must happen before push-prereq/build/push so the resource has
        // a VCR container registry annotation and linux/amd64 build target; Vercel deploy must
        // then wait for the resource-specific push step.
        var entries = GetDeploymentEntries(context.Model, environment).ToArray();
        if (entries.Length == 0)
        {
            return;
        }

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
            }

            var pushSteps = context.GetSteps(entry.Resource, WellKnownPipelineTags.PushContainerImage).ToArray();
            foreach (var pushStep in pushSteps)
            {
                AddUnique(pushStep.DependsOnSteps, prereqStepName);
                AddUnique(pushStep.RequiredBySteps, WellKnownPipelineSteps.Deploy);
            }

            // Aspire's global push prerequisite validates that every pushed image has a
            // registry. Vercel discovers the project-owned VCR registry through `vercel pull`,
            // so that validation must run after the Vercel prereq has annotated the resources.
            foreach (var pushPrereqStep in pushPrereqSteps)
            {
                AddUnique(pushPrereqStep.DependsOnSteps, prereqStepName);
            }

            foreach (var deployStep in deploySteps)
            {
                foreach (var pushStep in pushSteps)
                {
                    AddUnique(deployStep.DependsOnSteps, pushStep.Name);
                }
            }
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
        var preparedEntry = await PrepareDeploymentEntryAsync(context, entry).ConfigureAwait(false);
        var projectLink = GetVercelProjectLink(preparedEntry);
        var previousDeployment = await GetPreviousDeploymentStateEntryAsync(
            context,
            environment,
            options,
            preparedEntry.Resource.Name,
            projectLink.ProjectName).ConfigureAwait(false);
        // A checked-in .vercel/project.json means the user linked an existing provider project,
        // so deploy can target it but destroy must not claim ownership of it.
        bool managedByAspire = !HasVercelProjectLinkFile(entry.SourceRoot);

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
            var environmentConfiguration = await GetVercelEnvironmentConfigurationAsync(
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
            var oidcClaims = DecodeUnvalidatedOidcClaims(pulledProject.OidcToken);

            return new(environmentConfiguration, pulledProject, oidcClaims);
        }
        finally
        {
            // `vercel pull` can write provider-owned env/config files into the linked
            // directory. They may contain project secrets unrelated to the AppHost, so the
            // scratch link is never copied, logged, stored in state, or left on disk.
            DeleteDirectoryIfExists(projectLinkDirectory);
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
        await WriteBuildOutputAsync(entry, projectContext.PulledProject, image.Reference, context.CancellationToken).ConfigureAwait(false);

        string[] deployArguments = BuildDeployArguments(
            options,
            entry.DeployDirectory,
            GetVercelProjectOption(entry),
            projectContext.EnvironmentConfiguration.DeploymentEnvironmentVariables);

        var result = await runner.RunAsync(VercelCliFileName, deployArguments, entry.DeployDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"deploy prebuilt resource '{entry.Resource.Name}' to Vercel", VercelCliFileName, result);
        }

        var deploymentResult = GetDeploymentResult(result.StandardOutput);
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

        string[] arguments = BuildDockerInspectDigestArguments(preparedDeployment.TaggedImageReference);
        var result = await runner.RunAsync(DockerCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"resolve pushed VCR image digest for resource '{preparedDeployment.Entry.Resource.Name}'", DockerCliFileName, result);
        }

        string digest = GetDockerImageDigest(result.StandardOutput);
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
            BuildOutputApiVersion = VercelBuildOutputApiVersion,
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

    private static void RemoveAnnotations<TAnnotation>(IResource resource, Func<TAnnotation, bool>? predicate = null)
        where TAnnotation : IResourceAnnotation
    {
        foreach (var annotation in resource.Annotations.OfType<TAnnotation>().Where(annotation => predicate?.Invoke(annotation) ?? true).ToArray())
        {
            resource.Annotations.Remove(annotation);
        }
    }

    internal static string[] BuildDeployArguments(VercelEnvironmentOptionsAnnotation options, VercelDeploymentEntry entry)
        => BuildDeployArguments(options, GetDeployDirectory(entry), GetVercelProjectOption(entry), environmentVariables: []);

    // Keep CLI argument construction as pure array-returning helpers. Tests assert exact
    // argument boundaries so Vercel quirks such as `env add` requiring --cwd, not --project,
    // cannot regress into shell-quoted or source-mutating command strings.
    internal static string[] BuildDockerInspectDigestArguments(string imageReference)
        => ["buildx", "imagetools", "inspect", "--format", "{{json .Manifest}}", imageReference];

    internal static string[] BuildDestroyProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("remove");
        arguments.Add(projectName);
        AddOptionalScopeArgument(arguments, options);

        return [.. arguments];
    }

    internal static string[] BuildListProjectEnvironmentVariablesArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("env");
        arguments.Add("ls");
        arguments.Add(targetEnvironment);
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--format=json");

        return [.. arguments];
    }

    internal static string[] BuildAddProjectArguments(VercelEnvironmentOptionsAnnotation options, string projectName)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("add");
        arguments.Add(projectName);
        AddOptionalScopeArgument(arguments, options);

        return [.. arguments];
    }

    internal static string[] BuildAddProjectEnvironmentVariableArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("env");
        arguments.Add("add");
        arguments.Add(name);
        arguments.Add(targetEnvironment);
        AddOptionalScopeArgument(arguments, options);
        // Vercel env commands must run inside a linked project directory. Use the
        // Aspire-owned scratch link instead of the source root so provider metadata
        // and pulled env files never mutate the user's checkout.
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--force");
        arguments.Add("--sensitive");

        return [.. arguments];
    }

    internal static string[] BuildRemoveProjectEnvironmentVariableArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("env");
        arguments.Add("rm");
        arguments.Add(name);
        arguments.Add(targetEnvironment);
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");

        return [.. arguments];
    }

    internal static string[] BuildLinkProjectArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string projectNameOrId)
    {
        List<string> arguments = [];

        arguments.Add("link");
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--project");
        arguments.Add(projectNameOrId);

        return [.. arguments];
    }

    internal static string[] BuildPullProjectSettingsArguments(
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string targetEnvironment)
    {
        List<string> arguments = [];

        arguments.Add("pull");
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(projectLinkDirectory);
        arguments.Add("--yes");
        arguments.Add("--environment");
        arguments.Add(targetEnvironment);

        return [.. arguments];
    }

    internal static string[] BuildDockerLoginArguments(string username)
        => ["login", VcrRegistry, "--username", username, "--password-stdin"];

    private static void AddOptionalProjectArgument(List<string> arguments, string? projectNameOrId)
    {
        if (!string.IsNullOrWhiteSpace(projectNameOrId))
        {
            arguments.Add("--project");
            arguments.Add(projectNameOrId);
        }
    }

    private static void AddOptionalScopeArgument(List<string> arguments, VercelEnvironmentOptionsAnnotation options)
    {
        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            arguments.Add("--scope");
            arguments.Add(options.Scope);
        }
    }

    internal static string[] BuildInspectDeploymentArguments(VercelEnvironmentOptionsAnnotation options, string deploymentUrl)
    {
        List<string> arguments = [];

        arguments.Add("inspect");
        arguments.Add(deploymentUrl);
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--wait");
        arguments.Add("--timeout");
        arguments.Add("120s");
        arguments.Add("--format=json");

        return [.. arguments];
    }

    internal static string[] BuildValidateScopeArguments(VercelEnvironmentOptionsAnnotation options)
        => BuildListProjectsArguments(options);

    internal static string[] BuildListProjectsArguments(VercelEnvironmentOptionsAnnotation options, string? filter = null)
    {
        List<string> arguments = [];

        arguments.Add("project");
        arguments.Add("ls");
        AddOptionalScopeArgument(arguments, options);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            arguments.Add("--filter");
            arguments.Add(filter);
        }

        arguments.Add("--format=json");

        return [.. arguments];
    }

    private static async Task VerifyDeploymentAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        VercelDeploymentResult deploymentResult)
    {
        // A successful `vercel deploy` only means the CLI accepted the submission.
        // Query the provider before recording state so Aspire does not persist failed
        // or still-building deployments as successfully applied resources.
        string[] arguments = BuildInspectDeploymentArguments(options, deploymentResult.DeploymentUrl);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw CreateCliException($"verify Vercel deployment for resource '{resourceName}'", VercelCliFileName, result);
        }

        var inspection = GetDeploymentInspection(result.StandardOutput);
        if (inspection.ReadyState is null)
        {
            throw new DistributedApplicationException($"Vercel inspect output for resource '{resourceName}' did not include a deployment ready state. Output: {GetTrimmedOutput(result.StandardOutput)}");
        }

        if (!string.Equals(inspection.ReadyState, "READY", StringComparison.OrdinalIgnoreCase))
        {
            throw new DistributedApplicationException($"Vercel deployment for resource '{resourceName}' finished with state '{inspection.ReadyState}' instead of 'READY'.");
        }
    }

    public static async Task DestroyAsync(PipelineStepContext context, VercelEnvironmentResource environment)
    {
        var options = environment.GetVercelOptions();
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var state = ReadDeploymentState(stateSection);
        if (state is null)
        {
            // Keep no-op destroy cheap and offline. If Aspire has no deployment state,
            // there is no recorded provider object that this integration owns.
            context.Summary.Add("Vercel destroy", $"No Vercel deployment state was found for environment '{environment.Name}'. Nothing to destroy.");
            return;
        }

        ValidateDeploymentState(environment, options, state);

        // Destroy is state-first rather than model-first. The current AppHost may no
        // longer contain the resources that created these Vercel projects, but persisted
        // state records which provider objects Aspire is allowed to delete.
        var projects = state.Deployments
            .Where(static deployment => deployment.ManagedByAspire)
            .Select(static deployment => deployment.ProjectName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var linkedDeploymentsWithEnvironmentVariables = state.Deployments
            .Where(static deployment => !deployment.ManagedByAspire && deployment.ProjectEnvironmentVariables.Length > 0)
            .OrderBy(static deployment => deployment.ProjectName, StringComparer.Ordinal)
            .ThenBy(static deployment => deployment.ResourceName, StringComparer.Ordinal)
            .ToArray();

        if (projects.Length == 0 && linkedDeploymentsWithEnvironmentVariables.Length == 0)
        {
            // Linked .vercel projects are valid deploy targets but remain externally owned,
            // so clearing state is safer than deleting provider projects the user brought.
            context.Summary.Add("Vercel destroy", $"No Aspire-managed Vercel deployments were found for environment '{environment.Name}'.");
            await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
            return;
        }

        // Validate auth only after we know there is provider state to mutate. This keeps
        // `aspire destroy` usable in clean workspaces or after state has already been removed.
        await ValidateCliPrerequisitesAsync(context, environment).ConfigureAwait(false);
        var runner = context.Services.GetRequiredService<IVercelCliRunner>();

        foreach (var deployment in linkedDeploymentsWithEnvironmentVariables)
        {
            await RemoveLinkedProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                environment,
                deployment,
                GetVercelProjectEnvironmentName(state)).ConfigureAwait(false);
        }

        if (projects.Length == 0)
        {
            context.Summary.Add("Vercel destroy", $"No Aspire-managed Vercel deployments were found for environment '{environment.Name}'.");
            await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (string projectName in projects)
        {
            if (!await ProjectExistsAsync(context, runner, options, projectName).ConfigureAwait(false))
            {
                context.Summary.Add("Vercel project already absent", projectName);
            }
            else
            {
                string[] arguments = BuildDestroyProjectArguments(options, projectName);
                var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken, standardInput: "y\n").ConfigureAwait(false);

                if (!result.Succeeded)
                {
                    if (await ProjectExistsAsync(context, runner, options, projectName).ConfigureAwait(false))
                    {
                        throw CreateCliException($"destroy Vercel project '{projectName}'", VercelCliFileName, result);
                    }

                    context.Summary.Add("Vercel project already absent", projectName);
                }
                else
                {
                    context.Summary.Add("Vercel project removed", projectName);
                }
            }

            state = RemoveManagedProjectFromDeploymentState(state, projectName);
            // Save after each project removal so a later CLI failure leaves retryable state
            // for projects that still exist instead of forgetting partially cleaned resources.
            stateSection.SetValue(JsonSerializer.Serialize(state, JsonOptions));
            await stateManager.SaveSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
        }

        await stateManager.DeleteSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        CancellationToken cancellationToken)
        => await BuildDeployArgumentsAsync(
            executionContext,
            logger,
            options,
            entry,
            [entry],
            cancellationToken).ConfigureAwait(false);

    internal static async Task<string[]> BuildDeployArgumentsAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        // Publish plans must be useful without Vercel credentials or secret resolution.
        // Secret-bearing values are reduced to names/placeholders here; deploy resolves them
        // only when they are sent to Vercel's project env store over stdin.
        var entriesByResourceName = GetDeploymentEntriesByResourceName(entries);
        var environmentConfiguration = await GetVercelEnvironmentConfigurationAsync(
            executionContext,
            logger,
            options,
            entry,
            entriesByResourceName,
            resolveProjectEnvironmentVariableValues: false,
            cancellationToken).ConfigureAwait(false);

        return BuildDeployArguments(options, GetDeployDirectory(entry), GetVercelProjectOption(entry), environmentConfiguration.DeploymentEnvironmentVariables);
    }

    private static async Task<VercelDeploymentPlanEntry[]> CreateDeploymentPlanEntriesAsync(
        DistributedApplicationExecutionContext? executionContext,
        ILogger? logger,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyList<VercelDeploymentEntry> entries,
        CancellationToken cancellationToken)
    {
        List<VercelDeploymentPlanEntry> planEntries = [];
        var entriesByResourceName = GetDeploymentEntriesByResourceName(entries);

        foreach (var entry in entries)
        {
            // When execution context is available, include the same target-native env names
            // deploy will use. Values stay redacted because publish output is committed or
            // handed off more often than deploy logs.
            var environmentConfiguration = executionContext is null || logger is null
                ? VercelEnvironmentConfiguration.Empty
                : await GetVercelEnvironmentConfigurationAsync(executionContext, logger, options, entry, entriesByResourceName, resolveProjectEnvironmentVariableValues: false, cancellationToken).ConfigureAwait(false);

            planEntries.Add(new(
                entry.Resource.Name,
                GetDisplayDockerfilePath(entry),
                BuildDisplayDeployCommand(options, entry.Resource.Name, environmentConfiguration.DeploymentEnvironmentVariables),
                [.. environmentConfiguration.AllEnvironmentVariableNames.Order(StringComparer.Ordinal)]));
        }

        return [.. planEntries];
    }

    private static async Task<VercelEnvironmentConfiguration> GetVercelEnvironmentConfigurationAsync(
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        bool resolveProjectEnvironmentVariableValues,
        CancellationToken cancellationToken)
    {
        var executionConfiguration = await ExecutionConfigurationBuilder
            .Create(entry.Resource)
            .WithEnvironmentVariablesConfig()
            .WithArgumentsConfig()
            .BuildAsync(executionContext, logger, cancellationToken)
            .ConfigureAwait(false);

        if (executionConfiguration.Exception is not null)
        {
            throw new DistributedApplicationException($"Failed to process deployment configuration for resource '{entry.Resource.Name}'.", executionConfiguration.Exception);
        }

        ValidateUnsupportedRuntimeConfiguration(entry.Resource, executionConfiguration);

        var environmentVariables = await GetVercelEnvironmentConfigurationAsync(
            entry.Resource,
            options,
            executionConfiguration,
            entriesByResourceName,
            resolveProjectEnvironmentVariableValues,
            cancellationToken).ConfigureAwait(false);

        return environmentVariables;
    }

    private static string[] BuildDeployArguments(
        VercelEnvironmentOptionsAnnotation options,
        string deployDirectory,
        string? projectNameOrId,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        List<string> arguments = [];

        arguments.Add("deploy");
        AddOptionalScopeArgument(arguments, options);
        arguments.Add("--cwd");
        arguments.Add(deployDirectory);
        AddOptionalProjectArgument(arguments, projectNameOrId);
        arguments.Add("--prebuilt");
        arguments.Add("--yes");

        if (options.Production)
        {
            arguments.Add("--prod");
        }

        if (!string.IsNullOrWhiteSpace(options.Target))
        {
            arguments.Add("--target");
            arguments.Add(options.Target);
        }

        foreach (var environmentVariable in environmentVariables.OrderBy(static variable => variable.Key, StringComparer.Ordinal))
        {
            arguments.Add("--env");
            arguments.Add($"{environmentVariable.Key}={environmentVariable.Value}");
        }

        return [.. arguments];
    }

    private static string BuildDisplayDeployCommand(
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables)
    {
        // The plan should explain the command shape without leaking concrete source roots,
        // project names, or environment values that may be machine- or account-specific.
        var displayEnvironmentVariables = environmentVariables
            .Select(static environmentVariable => new KeyValuePair<string, string>(environmentVariable.Key, "<value>"))
            .ToArray();

        string displayImage = $"vcr.vercel.com/<owner>/<project>/{VercelContainerServiceName}:<tag>";
        string displayDeployDirectory = $"<{resourceName}-build-output>";
        string displayProject = $"<{resourceName}-vercel-project>";
        return $"vercel pull --cwd <{resourceName}-vercel-project-link> --yes --environment {GetVercelProjectEnvironmentName(options)} && aspire build/push {resourceName} -> {displayImage} && docker {string.Join(" ", BuildDockerInspectDigestArguments(displayImage))} && vercel {string.Join(" ", BuildDeployArguments(options, displayDeployDirectory, displayProject, displayEnvironmentVariables))}";
    }

    private static async Task ValidateExistingDeploymentStateAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = ReadDeploymentState(stateSection);
        if (existingState is not null)
        {
            ValidateDeploymentState(environment, options, existingState);
        }
    }

    private static async Task<PreviousVercelDeployment?> GetPreviousDeploymentStateEntryAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        string resourceName,
        string projectName)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = ReadDeploymentState(stateSection);
        if (existingState is null)
        {
            return null;
        }

        ValidateDeploymentState(environment, options, existingState);
        var entry = existingState.Deployments.FirstOrDefault(deployment =>
            string.Equals(deployment.ResourceName, resourceName, StringComparison.Ordinal)
            && string.Equals(deployment.ProjectName, projectName, StringComparison.Ordinal));

        return entry is null
            ? null
            : new(entry, GetVercelProjectEnvironmentName(existingState));
    }

    private static async Task SaveDeploymentStateEntryAsync(
        PipelineStepContext context,
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentStateEntry deployment)
    {
        var stateManager = context.Services.GetRequiredService<IDeploymentStateManager>();
        var stateSection = await stateManager.AcquireSectionAsync(GetStateSectionName(environment), context.CancellationToken).ConfigureAwait(false);
        var existingState = ReadDeploymentState(stateSection);
        var state = existingState is null
            ? CreateDeploymentState(environment, options, [deployment])
            : MergeDeploymentState(environment, options, existingState, deployment);

        stateSection.SetValue(JsonSerializer.Serialize(state, JsonOptions));

        await stateManager.SaveSectionAsync(stateSection, context.CancellationToken).ConfigureAwait(false);
    }

    private static VercelDeploymentState CreateDeploymentState(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentStateEntry[] deployments)
        => new(
            DeploymentStateSchemaVersion,
            environment.Name,
            NormalizeScope(options.Scope),
            NormalizeTarget(options.Target),
            options.Production,
            deployments);

    private static VercelDeploymentState MergeDeploymentState(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentState existingState,
        VercelDeploymentStateEntry deployment)
    {
        ValidateDeploymentState(environment, options, existingState);

        return CreateDeploymentState(
            environment,
            options,
            [
                .. existingState.Deployments.Where(existing =>
                    !string.Equals(existing.ResourceName, deployment.ResourceName, StringComparison.Ordinal)
                    || !string.Equals(existing.ProjectName, deployment.ProjectName, StringComparison.Ordinal)),
                deployment
            ]);
    }

    private static VercelDeploymentState? ReadDeploymentState(DeploymentStateSection stateSection)
    {
        // DeploymentStateSection storage shape has changed across Aspire builds. Accept the
        // known wrappers so destroy can still clean up projects created by an older CLI.
        if (stateSection.Data.TryGetPropertyValue("value", out JsonNode? value)
            && value is not null)
        {
            return DeserializeDeploymentState(value);
        }

        value = stateSection.Data.FirstOrDefault().Value;
        if (value is not null)
        {
            return DeserializeDeploymentState(value);
        }

        if (stateSection.Data.ContainsKey("schemaVersion"))
        {
            return stateSection.Data.Deserialize<VercelDeploymentState>(JsonOptions);
        }

        return null;
    }

    private static VercelDeploymentState? DeserializeDeploymentState(JsonNode value)
    {
        return value.GetValueKind() == JsonValueKind.String
            ? JsonSerializer.Deserialize<VercelDeploymentState>(value.GetValue<string>(), JsonOptions)
            : value.Deserialize<VercelDeploymentState>(JsonOptions);
    }

    private static void ValidateDeploymentState(
        VercelEnvironmentResource environment,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentState state)
    {
        if (state.SchemaVersion != DeploymentStateSchemaVersion)
        {
            throw new DistributedApplicationException($"Vercel deployment state for environment '{environment.Name}' uses unsupported schema version '{state.SchemaVersion}'. Redeploy the environment before destroying it.");
        }

        if (!string.Equals(state.Environment, environment.Name, StringComparison.Ordinal))
        {
            throw new DistributedApplicationException($"Vercel deployment state for environment '{state.Environment}' cannot be used to destroy environment '{environment.Name}'.");
        }

        string? configuredScope = NormalizeScope(options.Scope);
        if (!string.Equals(state.Scope, configuredScope, StringComparison.Ordinal))
        {
            string stateScope = string.IsNullOrWhiteSpace(state.Scope) ? "<default>" : state.Scope;
            string requestedScope = string.IsNullOrWhiteSpace(configuredScope) ? "<default>" : configuredScope;
            throw new DistributedApplicationException($"Vercel deployment state for environment '{environment.Name}' was created for scope '{stateScope}', but destroy is configured for scope '{requestedScope}'. Use the same Vercel scope that created the deployment state.");
        }
    }

    private static string? NormalizeScope(string? scope)
        => string.IsNullOrWhiteSpace(scope) ? null : scope;

    private static string? NormalizeTarget(string? target)
        => string.IsNullOrWhiteSpace(target) ? null : target;

    private static string? GetProductionUrl(VercelEnvironmentOptionsAnnotation options, string projectName)
        => options.Production ? $"https://{projectName}.vercel.app" : null;

    private static string GetDeployDirectory(VercelDeploymentEntry entry)
        => string.IsNullOrWhiteSpace(entry.DeployDirectory) ? entry.SourceRoot : entry.DeployDirectory;

    private static VercelDeploymentState RemoveManagedProjectFromDeploymentState(VercelDeploymentState state, string projectName)
        => state with
        {
            Deployments = state.Deployments
                .Where(deployment => !deployment.ManagedByAspire || !string.Equals(deployment.ProjectName, projectName, StringComparison.Ordinal))
                .ToArray()
        };

    private static string GetStateSectionName(VercelEnvironmentResource environment) => $"{StateSectionNamePrefix}{environment.Name}";

    private static async Task<bool> ProjectExistsAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string projectName)
    {
        // Avoid treating localized or reformatted CLI errors as provider state. The Vercel
        // CLI exposes project lists as JSON, so destroy checks exact project names before
        // deleting and again after a failed delete to distinguish races from real failures.
        string[] arguments = BuildListProjectsArguments(options, projectName);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"list Vercel projects while checking for '{projectName}'", VercelCliFileName, result);
        }

        return ProjectListContainsProject(result.StandardOutput, projectName);
    }

    private static async Task<bool> ProjectEnvironmentVariableExistsAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string name,
        string targetEnvironment)
    {
        // `vercel env rm` reports absence as human text, but `vercel env ls --format=json`
        // returns the linked project's exact keys. Use that provider read for idempotent
        // stale-secret cleanup instead of parsing failure prose.
        string[] arguments = BuildListProjectEnvironmentVariablesArguments(options, projectLinkDirectory, targetEnvironment);
        var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"list Vercel project environment variables before removing '{name}'", VercelCliFileName, result);
        }

        return EnvironmentVariableListContainsName(result.StandardOutput, name);
    }

    private static async Task ConfigureProjectEnvironmentVariablesAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        string projectLinkDirectory,
        IReadOnlyList<KeyValuePair<string, string>> environmentVariables,
        PreviousVercelDeployment? previousDeployment)
    {
        var currentNames = environmentVariables
            .Select(static variable => variable.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (previousDeployment is not null)
        {
            string[] staleNames = previousDeployment.Entry.ProjectEnvironmentVariables
                .Where(name => !currentNames.Contains(name))
                .Order(StringComparer.Ordinal)
                .ToArray();

            await RemoveProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                projectLinkDirectory,
                entry.Resource.Name,
                staleNames,
                previousDeployment.ProjectEnvironment).ConfigureAwait(false);
        }

        if (environmentVariables.Count == 0)
        {
            return;
        }

        string targetEnvironment = GetVercelProjectEnvironmentName(options);
        foreach (var environmentVariable in environmentVariables.OrderBy(static variable => variable.Key, StringComparer.Ordinal))
        {
            string[] arguments = BuildAddProjectEnvironmentVariableArguments(options, projectLinkDirectory, environmentVariable.Key, targetEnvironment);
            var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken, standardInput: environmentVariable.Value).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw CreateCliException($"configure Vercel project environment variable '{environmentVariable.Key}' for resource '{entry.Resource.Name}'", VercelCliFileName, result);
            }
        }
    }

    private static async Task RemoveProjectEnvironmentVariablesAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        string projectLinkDirectory,
        string resourceName,
        IReadOnlyList<string> names,
        string targetEnvironment)
    {
        foreach (string name in names)
        {
            if (!await ProjectEnvironmentVariableExistsAsync(context, runner, options, projectLinkDirectory, name, targetEnvironment).ConfigureAwait(false))
            {
                continue;
            }

            string[] arguments = BuildRemoveProjectEnvironmentVariableArguments(options, projectLinkDirectory, name, targetEnvironment);
            var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
            if (!result.Succeeded
                && await ProjectEnvironmentVariableExistsAsync(context, runner, options, projectLinkDirectory, name, targetEnvironment).ConfigureAwait(false))
            {
                throw CreateCliException($"remove stale Vercel project environment variable '{name}' for resource '{resourceName}'", VercelCliFileName, result);
            }
        }
    }

    private static async Task RemoveLinkedProjectEnvironmentVariablesAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelEnvironmentResource environment,
        VercelDeploymentStateEntry deployment,
        string targetEnvironment)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string projectLinkDirectory = Path.Combine(outputService.GetTempDirectory(environment), ".vercel-projects", deployment.ProjectName);
        DeleteDirectoryIfExists(projectLinkDirectory);
        Directory.CreateDirectory(projectLinkDirectory);

        try
        {
            string[] linkArguments = BuildLinkProjectArguments(options, projectLinkDirectory, deployment.ProjectId ?? deployment.ProjectName);
            var linkResult = await runner.RunAsync(VercelCliFileName, linkArguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
            if (!linkResult.Succeeded)
            {
                throw CreateCliException($"link Vercel project '{deployment.ProjectName}' for environment variable cleanup", VercelCliFileName, linkResult);
            }

            await RemoveProjectEnvironmentVariablesAsync(
                context,
                runner,
                options,
                projectLinkDirectory,
                deployment.ResourceName,
                deployment.ProjectEnvironmentVariables,
                targetEnvironment).ConfigureAwait(false);
        }
        finally
        {
            DeleteDirectoryIfExists(projectLinkDirectory);
        }
    }

    private static async Task<string> PrepareProjectEnvironmentDirectoryAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string projectLinkDirectory = Path.Combine(outputService.GetTempDirectory(entry.Resource), ".vercel-project");
        if (Directory.Exists(projectLinkDirectory))
        {
            Directory.Delete(projectLinkDirectory, recursive: true);
        }

        Directory.CreateDirectory(projectLinkDirectory);

        // `vercel env add` is project-scoped but intentionally does not accept --project.
        // Link a scratch directory instead of the source root so secret configuration can use
        // the CLI's native project lookup without writing .vercel metadata into user code.
        string[] linkArguments = BuildLinkProjectArguments(options, projectLinkDirectory, GetVercelProjectOption(entry));
        var result = await runner.RunAsync(VercelCliFileName, linkArguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"prepare temporary Vercel project link for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }

        return projectLinkDirectory;
    }

    private static async Task<VercelPulledProject> PullProjectSettingsAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        string projectLinkDirectory)
    {
        string targetEnvironment = GetVercelProjectEnvironmentName(options);
        string[] arguments = BuildPullProjectSettingsArguments(options, projectLinkDirectory, targetEnvironment);
        var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"pull Vercel project settings for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }

        string vercelDirectory = Path.Combine(projectLinkDirectory, VercelDirectoryName);
        string projectJsonPath = Path.Combine(vercelDirectory, VercelProjectFileName);
        string environmentPath = Path.Combine(vercelDirectory, $".env.{targetEnvironment}.local");

        if (!File.Exists(projectJsonPath))
        {
            throw new DistributedApplicationException($"Vercel pull did not write expected project settings file '{projectJsonPath}' for resource '{entry.Resource.Name}'.");
        }

        if (!File.Exists(environmentPath))
        {
            throw new DistributedApplicationException($"Vercel pull did not write expected environment file '{environmentPath}' for resource '{entry.Resource.Name}'.");
        }

        var environmentVariables = ParseDotEnvFile(await File.ReadAllLinesAsync(environmentPath, context.CancellationToken).ConfigureAwait(false));
        if (!environmentVariables.TryGetValue(VercelOidcTokenEnvironmentVariable, out string? oidcToken)
            || string.IsNullOrWhiteSpace(oidcToken))
        {
            throw new DistributedApplicationException($"Vercel pull did not provide {VercelOidcTokenEnvironmentVariable}, which is required to authenticate local Docker builds to VCR.");
        }

        string projectJsonContent = await File.ReadAllTextAsync(projectJsonPath, context.CancellationToken).ConfigureAwait(false);
        var project = ReadVercelProjectSettings(projectJsonPath, projectJsonContent);

        // `vercel pull` materializes project secrets next to the scratch link so local
        // builders can read them. This integration only needs the short-lived OIDC token
        // and project metadata; delete the env files before creating deploy artifacts.
        DeleteIfExists(environmentPath);
        DeleteIfExists(Path.Combine(projectLinkDirectory, ".env.local"));

        return new(project.ProjectName, project.ProjectId, project.OrgId, projectJsonContent, oidcToken);
    }

    private static async Task LoginToVcrAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        string oidcToken,
        VercelOidcClaims claims)
        => await LoginToVcrAsync(runner, oidcToken, claims, context.CancellationToken).ConfigureAwait(false);

    internal static async Task LoginToVcrAsync(
        IVercelCliRunner runner,
        string oidcToken,
        VercelOidcClaims claims,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claims.OwnerId))
        {
            throw new DistributedApplicationException("The Vercel OIDC token did not include the owner_id claim required to authenticate to VCR.");
        }

        string[] arguments = BuildDockerLoginArguments(claims.OwnerId);
        var result = await runner.RunAsync(DockerCliFileName, arguments, workingDirectory: null, cancellationToken, standardInput: oidcToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException("authenticate Docker to VCR", DockerCliFileName, result);
        }
    }

    internal static async Task WriteBuildOutputAsync(
        VercelDeploymentEntry entry,
        VercelPulledProject project,
        string imageReference,
        CancellationToken cancellationToken)
    {
        // Vercel Build Output API v3 expects:
        //   .vercel/project.json                          copied project identity from `vercel pull`
        //   .vercel/output/config.json                    routes and API version
        //   .vercel/output/functions/index.func/.vc-config.json
        //       { "runtime": "container", "handler": "<vcr image>@sha256:..." }
        // There is intentionally no user source copy here; Aspire's build/push pipeline has
        // already built the image, and Vercel deploy uploads only metadata that points at it.
        string vercelDirectory = Path.Combine(entry.DeployDirectory, VercelDirectoryName);
        string outputDirectory = Path.Combine(vercelDirectory, VercelOutputDirectoryName);
        string functionDirectory = Path.Combine(outputDirectory, "functions", "index.func");
        Directory.CreateDirectory(functionDirectory);

        await File.WriteAllTextAsync(Path.Combine(vercelDirectory, VercelProjectFileName), project.ProjectJsonContent, cancellationToken).ConfigureAwait(false);

        var outputConfig = new JsonObject
        {
            ["version"] = VercelBuildOutputApiVersion,
            ["routes"] = new JsonArray
            {
                new JsonObject
                {
                    ["handle"] = "filesystem"
                },
                new JsonObject
                {
                    ["src"] = "/(.*)",
                    ["dest"] = "/index"
                }
            }
        };

        var functionConfig = new JsonObject
        {
            ["handler"] = imageReference,
            ["runtime"] = "container",
            ["environment"] = new JsonObject()
        };

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "config.json"), outputConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(functionDirectory, ".vc-config.json"), functionConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private static VercelPulledProjectSettings ReadVercelProjectSettings(string projectJsonPath, string projectJsonContent)
    {
        try
        {
            // `vercel pull` writes `.vercel/project.json`. Only project identity fields are
            // needed: they select the linked provider project and are safe to persist in state.
            using var document = JsonDocument.Parse(projectJsonContent);
            var root = document.RootElement;

            string projectName = root.TryGetProperty("projectName", out var projectNameElement) && projectNameElement.ValueKind == JsonValueKind.String
                ? projectNameElement.GetString() ?? string.Empty
                : string.Empty;
            string? projectId = root.TryGetProperty("projectId", out var projectIdElement) && projectIdElement.ValueKind == JsonValueKind.String
                ? projectIdElement.GetString()
                : null;
            string? orgId = root.TryGetProperty("orgId", out var orgIdElement) && orgIdElement.ValueKind == JsonValueKind.String
                ? orgIdElement.GetString()
                : null;

            return new(projectName, projectId, orgId);
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException($"Vercel project settings file '{projectJsonPath}' is invalid JSON.", ex);
        }
    }

    internal static string GetDockerImageDigest(string output)
    {
        string trimmed = output.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            try
            {
                // `docker buildx imagetools inspect --format '{{json .Digest}}'` style output
                // is a JSON string. Older experiments used this shape before Vercel required
                // selecting the concrete linux/amd64 manifest from the OCI index.
                string? digest = JsonSerializer.Deserialize<string>(trimmed);
                if (IsSha256Digest(digest))
                {
                    return digest!;
                }
            }
            catch (JsonException ex)
            {
                throw new DistributedApplicationException("Docker returned invalid JSON while resolving the pushed VCR image digest.", ex);
            }
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                // Current path uses `--format '{{json .Manifest}}'`. Docker may return an OCI
                // image index with a `manifests[]` array, or a single manifest object. Vercel
                // rejected index digests in live smoke tests, so prefer the linux/amd64 child.
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;

                if (root.TryGetProperty("manifests", out var manifests) && manifests.ValueKind == JsonValueKind.Array)
                {
                    foreach (var manifest in manifests.EnumerateArray())
                    {
                        if (manifest.TryGetProperty("platform", out var platform)
                            && platform.TryGetProperty("os", out var osElement)
                            && platform.TryGetProperty("architecture", out var architectureElement)
                            && string.Equals(osElement.GetString(), "linux", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(architectureElement.GetString(), "amd64", StringComparison.OrdinalIgnoreCase)
                            && TryGetJsonString(manifest, "digest", out var platformDigest)
                            && IsSha256Digest(platformDigest))
                        {
                            return platformDigest!;
                        }
                    }

                    throw new DistributedApplicationException("Docker did not return a linux/amd64 manifest digest for the pushed VCR image. Vercel requires linux/amd64 container images.");
                }

                if (TryGetJsonString(root, "digest", out var digest) && IsSha256Digest(digest))
                {
                    return digest!;
                }
            }
            catch (JsonException ex)
            {
                throw new DistributedApplicationException("Docker returned invalid JSON while resolving the pushed VCR image digest.", ex);
            }
        }

        var match = Regex.Match(trimmed, @"sha256:[a-fA-F0-9]{64}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (match.Success)
        {
            return match.Value;
        }

        throw new DistributedApplicationException($"Docker did not return a valid sha256 image digest. Output: {GetTrimmedOutput(output)}");
    }

    private static bool TryGetJsonString(JsonElement element, string propertyName, [NotNullWhen(true)] out string? value)
    {
        value = element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

        return !string.IsNullOrWhiteSpace(value);
    }

    internal static VercelOidcClaims DecodeUnvalidatedOidcClaims(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new DistributedApplicationException("The Vercel OIDC token is not a valid compact JWT.");
        }

        try
        {
            // This is an unvalidated decode of the Vercel-issued token from `vercel pull`.
            // Docker/Vercel validate the token when it is used; here we only need routing
            // metadata such as owner_id/project to construct the VCR login and repository.
            byte[] payloadBytes = Convert.FromBase64String(PadBase64Url(parts[1]));
            using var document = JsonDocument.Parse(payloadBytes);
            var root = document.RootElement;

            return new(
                GetStringClaim(root, "owner_id"),
                GetStringClaim(root, "owner"),
                GetStringClaim(root, "project"),
                GetStringClaim(root, "project_id"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new DistributedApplicationException("The Vercel OIDC token payload could not be decoded.", ex);
        }
    }

    private static string? GetStringClaim(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static string PadBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        return padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
    }

    internal static Dictionary<string, string> ParseDotEnvFile(IEnumerable<string> lines)
    {
        // Vercel writes dotenv files such as `.vercel/.env.production.local` during pull.
        // We only need the VERCEL_OIDC_TOKEN line. This intentionally supports the subset the
        // CLI emits: comments/blank lines, KEY=value, single/double quoted values, and common
        // backslash escapes. It is not a general dotenv evaluator with interpolation.
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            values[key] = UnquoteDotEnvValue(value);
        }

        return values;
    }

    private static string UnquoteDotEnvValue(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return value.Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static bool IsSha256Digest([NotNullWhen(true)] string? value)
        => value is not null && Regex.IsMatch(value, "^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task EnsureManagedProjectAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry)
    {
        // `vercel project add` is idempotent for the current login/scope in the CLI versions
        // this integration supports: it creates the project or validates that it already
        // exists and is accessible. Failure here means deploy should not proceed to image push.
        string projectName = GetVercelProjectName(entry);
        string[] arguments = BuildAddProjectArguments(options, projectName);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"create or validate Vercel project '{projectName}' for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }
    }

    private static string GetVercelProjectEnvironmentName(VercelEnvironmentOptionsAnnotation options)
    {
        if (options.Production)
        {
            return "production";
        }

        return string.IsNullOrWhiteSpace(options.Target) ? "preview" : options.Target;
    }

    private static string GetVercelProjectEnvironmentName(VercelDeploymentState state)
    {
        if (state.Production)
        {
            return "production";
        }

        return string.IsNullOrWhiteSpace(state.Target) ? "preview" : state.Target;
    }

    private static async Task<VercelEnvironmentConfiguration> GetVercelEnvironmentConfigurationAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IExecutionConfigurationResult executionConfiguration,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        bool resolveProjectEnvironmentVariableValues,
        CancellationToken cancellationToken)
    {
        List<KeyValuePair<string, string>> deploymentEnvironmentVariables = [];
        List<KeyValuePair<string, string>> projectEnvironmentVariables = [];
        HashSet<string> names = new(StringComparer.Ordinal);

        // This is the env-var edge-case boundary. Vercel has deployment env args and
        // project env secrets, but no Aspire-style connection binding/service-discovery
        // object in this preview. Only deterministic production endpoint URLs are projected;
        // other resource references fail here instead of becoming misleading strings.
        // Keep a single pass over Aspire's unprocessed environment dictionary. The processed
        // value can contain publish-mode manifest expressions, but deployment needs the
        // original graph value so it can choose Vercel's concrete URL/secret mechanism.
        foreach (var environmentVariable in executionConfiguration.EnvironmentVariablesWithUnprocessed)
        {
            string name = environmentVariable.Key;
            object unprocessedValue = environmentVariable.Value.Item1;
            string value = environmentVariable.Value.Item2;

            ValidateEnvironmentVariableName(resource, name);
            if (!names.Add(name))
            {
                throw new DistributedApplicationException(
                    $"Resource '{resource.Name}' configures environment variable '{name}' more than once. Vercel project environment variable names must be unique.");
            }

            if (ContainsUnsupportedResourceReference(resource, unprocessedValue))
            {
                throw new DistributedApplicationException(
                    $"Environment variable '{name}' for resource '{resource.Name}' references another Aspire resource or service in a way that cannot be represented as a Vercel deployment URL. Use endpoint references to Vercel production workloads, or configure the value in Vercel project environment variables.");
            }

            // Non-secrets can ride on `vercel deploy --env`; secret-bearing values must use
            // Vercel project environment variables so values never appear in CLI arguments.
            bool containsSecret = ContainsSecretReference(unprocessedValue);
            if (containsSecret)
            {
                value = resolveProjectEnvironmentVariableValues
                    ? await GetVercelProjectEnvironmentVariableValueAsync(
                        resource,
                        options,
                        entriesByResourceName,
                        unprocessedValue,
                        value,
                        cancellationToken).ConfigureAwait(false)
                    : "<value>";
            }
            else if (TryGetVercelEnvironmentVariableValue(resource, options, entriesByResourceName, unprocessedValue, out string? vercelValue))
            {
                value = vercelValue;
            }

            if (containsSecret)
            {
                projectEnvironmentVariables.Add(new(name, value));
            }
            else
            {
                deploymentEnvironmentVariables.Add(new(name, value));
            }
        }

        return new(deploymentEnvironmentVariables, projectEnvironmentVariables);
    }

    private static async ValueTask<string> GetVercelProjectEnvironmentVariableValueAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        object? value,
        string processedValue,
        CancellationToken cancellationToken)
    {
        // This path resolves values for `vercel env add`, not `vercel deploy --env`.
        // Values can be secret-bearing because they are sent on stdin to Vercel's secret
        // store; they must not be copied into publish plans, command lines, or state.
        switch (value)
        {
            case null:
                return processedValue;
            case string stringValue:
                return stringValue;
            case ParameterResource parameter:
                return await GetParameterValueAsync(parameter, cancellationToken).ConfigureAwait(false);
            case IResourceBuilder<ParameterResource> parameterBuilder:
                return await GetParameterValueAsync(parameterBuilder.Resource, cancellationToken).ConfigureAwait(false);
            case IResourceWithConnectionString connectionStringResource:
                return await GetValueProviderValueAsync(connectionStringResource.ConnectionStringExpression, $"connection string for resource '{connectionStringResource.Name}'", cancellationToken).ConfigureAwait(false);
            case IResourceBuilder<IResourceWithConnectionString> connectionStringBuilder:
                return await GetValueProviderValueAsync(connectionStringBuilder.Resource.ConnectionStringExpression, $"connection string for resource '{connectionStringBuilder.Resource.Name}'", cancellationToken).ConfigureAwait(false);
            case ReferenceExpression referenceExpression:
                return await GetVercelProjectReferenceExpressionValueAsync(resource, options, entriesByResourceName, referenceExpression, cancellationToken).ConfigureAwait(false);
            case IValueProvider valueProvider:
                return await GetValueProviderValueAsync(valueProvider, "environment variable value", cancellationToken).ConfigureAwait(false);
            default:
                return processedValue;
        }
    }

    private static async ValueTask<string> GetVercelProjectReferenceExpressionValueAsync(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        ReferenceExpression referenceExpression,
        CancellationToken cancellationToken)
    {
        if (referenceExpression.IsConditional)
        {
            throw new DistributedApplicationException("Vercel project environment variables do not support conditional reference expressions. Configure a concrete Vercel project environment variable instead.");
        }

        var arguments = new object?[referenceExpression.ValueProviders.Count];
        for (int i = 0; i < referenceExpression.ValueProviders.Count; i++)
        {
            IValueProvider valueProvider = referenceExpression.ValueProviders[i];
            arguments[i] = valueProvider switch
            {
                // Secret-bearing project env vars may combine endpoint URLs with secret
                // parameters because the final value is sent through Vercel's secret path.
                EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url)),
                EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression),
                ParameterResource parameter => await GetParameterValueAsync(parameter, cancellationToken).ConfigureAwait(false),
                IResourceWithConnectionString connectionStringResource => await GetValueProviderValueAsync(connectionStringResource.ConnectionStringExpression, $"connection string for resource '{connectionStringResource.Name}'", cancellationToken).ConfigureAwait(false),
                _ => await GetValueProviderValueAsync(valueProvider, "reference expression value", cancellationToken).ConfigureAwait(false)
            };

            if (referenceExpression.StringFormats[i] is "uri" && arguments[i] is string stringValue)
            {
                arguments[i] = Uri.EscapeDataString(stringValue);
            }
        }

        return string.Format(CultureInfo.InvariantCulture, referenceExpression.Format, arguments);
    }

    private static async ValueTask<string> GetParameterValueAsync(ParameterResource parameter, CancellationToken cancellationToken)
    {
        try
        {
            string? value = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            return value ?? throw new DistributedApplicationException($"Secret parameter '{parameter.Name}' did not produce a value for Vercel project environment configuration.");
        }
        catch (MissingParameterValueException ex)
        {
            throw new DistributedApplicationException($"Secret parameter '{parameter.Name}' does not have a value. Provide a value before deploying to Vercel.", ex);
        }
    }

    private static async ValueTask<string> GetValueProviderValueAsync(IValueProvider valueProvider, string description, CancellationToken cancellationToken)
    {
        try
        {
            string? value = await valueProvider.GetValueAsync(cancellationToken).ConfigureAwait(false);
            return value ?? throw new DistributedApplicationException($"The {description} did not produce a value for Vercel project environment configuration.");
        }
        catch (MissingParameterValueException ex)
        {
            throw new DistributedApplicationException($"The {description} does not have a value. Provide a value before deploying to Vercel.", ex);
        }
    }

    private static bool TryGetVercelEnvironmentVariableValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        object? value,
        [NotNullWhen(true)] out string? vercelValue)
    {
        // Service-discovery env vars generated by WithReference also arrive as structured
        // endpoint values, so translate by value shape instead of by environment variable name.
        switch (value)
        {
            case EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference):
                vercelValue = GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url));
                return true;
            case EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint):
                vercelValue = GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression);
                return true;
            case ReferenceExpression referenceExpression when ContainsCrossResourceEndpointReference(resource, referenceExpression):
                vercelValue = GetVercelReferenceExpressionValue(resource, options, entriesByResourceName, referenceExpression);
                return true;
            default:
                vercelValue = null;
                return false;
        }
    }

    private static string GetVercelReferenceExpressionValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        ReferenceExpression referenceExpression)
    {
        if (referenceExpression.IsConditional)
        {
            throw new DistributedApplicationException("Vercel endpoint references do not support conditional reference expressions. Configure a concrete Vercel project environment variable instead.");
        }

        var arguments = new object?[referenceExpression.ValueProviders.Count];
        for (int i = 0; i < referenceExpression.ValueProviders.Count; i++)
        {
            IValueProvider valueProvider = referenceExpression.ValueProviders[i];
            arguments[i] = valueProvider switch
            {
                EndpointReference endpointReference when IsCrossResourceEndpointReference(resource, endpointReference) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReference.Property(EndpointProperty.Url)),
                EndpointReferenceExpression endpointReferenceExpression when IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint) => GetVercelEndpointPropertyValue(resource, options, entriesByResourceName, endpointReferenceExpression),
                // Mixed expressions can hide provider-specific ordering or secret semantics.
                // Keep this path to deterministic endpoint-only production URLs.
                _ => throw new DistributedApplicationException("Vercel endpoint reference expressions cannot be combined with parameters, secrets, or other value providers. Configure a concrete Vercel project environment variable instead.")
            };

            if (referenceExpression.StringFormats[i] is "uri" && arguments[i] is string stringValue)
            {
                arguments[i] = Uri.EscapeDataString(stringValue);
            }
        }

        return string.Format(CultureInfo.InvariantCulture, referenceExpression.Format, arguments);
    }

    private static string GetVercelEndpointPropertyValue(
        IResource resource,
        VercelEnvironmentOptionsAnnotation options,
        IReadOnlyDictionary<string, VercelDeploymentEntry> entriesByResourceName,
        EndpointReferenceExpression endpointReferenceExpression)
    {
        // This is the endpoint-reference edge-case boundary: preview/custom URLs are
        // post-deploy outputs, internal endpoints have no public Vercel edge address, and
        // cross-environment references lack a stable same-deploy alias. Fail before values
        // are written to Vercel env vars.
        if (!options.Production)
        {
            throw new DistributedApplicationException(
                "Vercel endpoint references require production deployments because preview and custom target URLs are assigned by Vercel after deployment. Call WithVercelProductionDeployments on the Vercel environment, or remove the reference.");
        }

        var endpointReference = endpointReferenceExpression.Endpoint;
        var endpoint = endpointReference.EndpointAnnotation;
        if (!endpoint.IsExternal)
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but Vercel endpoint references can only target external HTTP or HTTPS endpoints. Configure an external endpoint or remove the reference.");
        }

        if (!IsHttpEndpoint(endpoint))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}' with scheme '{endpoint.UriScheme}', but Vercel endpoint references support only HTTP or HTTPS endpoints.");
        }

        if (!entriesByResourceName.TryGetValue(endpointReference.Resource.Name, out var referencedEntry))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' references endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but the referenced resource does not target this Vercel environment. Vercel endpoint references can only target workloads deployed to the same Vercel environment.");
        }

        string host = $"{GetVercelProjectName(referencedEntry)}.vercel.app";
        const int port = 443;

        return endpointReferenceExpression.Property switch
        {
            // Production aliases are deterministic before deploy; preview/custom URLs are not.
            // Keep endpoint references on the stable caller-visible Vercel HTTPS surface.
            EndpointProperty.Url => $"https://{host}",
            EndpointProperty.Host or EndpointProperty.IPV4Host => host,
            EndpointProperty.Port => port.ToString(CultureInfo.InvariantCulture),
            EndpointProperty.TargetPort => endpoint.TargetPort is int targetPort
                ? targetPort.ToString(CultureInfo.InvariantCulture)
                : throw new DistributedApplicationException(
                    // Azure publishers can carry ContainerPortReference placeholders in
                    // Bicep/Helm. Vercel deploy receives concrete CLI env values, so an
                    // unresolved TargetPort would become a bogus string rather than a
                    // target-native reference.
                    $"Resource '{resource.Name}' references endpoint property '{EndpointProperty.TargetPort}' for endpoint '{endpoint.Name}' on resource '{endpointReference.Resource.Name}', but the endpoint does not define an explicit target port. Configure a target port or avoid passing TargetPort to Vercel."),
            EndpointProperty.Scheme => "https",
            EndpointProperty.HostAndPort => $"{host}:{port.ToString(CultureInfo.InvariantCulture)}",
            EndpointProperty.TlsEnabled => bool.TrueString,
            _ => throw new DistributedApplicationException($"The endpoint property '{endpointReferenceExpression.Property}' is not supported for Vercel endpoint references.")
        };
    }

    private static void ValidateEnvironmentVariableName(IResource resource, string name)
    {
        // Do not remap names to satisfy Vercel's env var shape. A lossy rename would break
        // the consuming workload's contract, so invalid names fail with the original key.
        if (string.IsNullOrWhiteSpace(name)
            || (!char.IsAsciiLetter(name[0]) && name[0] != '_')
            || name.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures invalid Vercel environment variable name '{name}'. Use letters, digits, and underscores, and start with a letter or underscore.");
        }
    }

    private static void ValidateUnsupportedRuntimeConfiguration(
        IResource resource,
        IExecutionConfigurationResult executionConfiguration)
    {
        // These Aspire concepts have no faithful Vercel Dockerfile-deploy equivalent in this
        // preview. Rejecting them is safer than silently dropping entrypoint, args, or build
        // values that would change the workload's deployed behavior. Aspire's built-in
        // image build/push pipeline owns build-time Docker options; this validation only
        // rejects runtime concepts the Vercel Build Output API path cannot preserve.
        if (resource is ContainerResource { Entrypoint: not null })
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures a container entrypoint, but Vercel Dockerfile deployments use the CMD/ENTRYPOINT from Aspire's publish output. Configure the workload's publish behavior or Vercel project settings instead.");
        }

        if (executionConfiguration.ArgumentsWithUnprocessed.Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire command-line arguments, but Vercel Dockerfile deployments cannot override Docker CMD/ENTRYPOINT. Configure the workload's publish behavior or express the values as environment variables.");
        }
    }

    private static bool ContainsSecretReference(object? value)
    {
        // Connection strings are treated as secret-bearing even when the underlying provider
        // does not mark each segment secret; Vercel should receive them through project env.
        return value switch
        {
            null => false,
            string => false,
            ParameterResource parameter => parameter.Secret,
            IResourceBuilder<ParameterResource> parameterBuilder => parameterBuilder.Resource.Secret,
            IResourceWithConnectionString => true,
            IResourceBuilder<IResourceWithConnectionString> => true,
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(ContainsSecretReference),
            _ => false
        };
    }

    private static bool ContainsCrossResourceEndpointReference(IResource resource, object? value)
    {
        return value switch
        {
            null => false,
            EndpointReference endpointReference => IsCrossResourceEndpointReference(resource, endpointReference),
            EndpointReferenceExpression endpointReferenceExpression => IsCrossResourceEndpointReference(resource, endpointReferenceExpression.Endpoint),
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(reference => ContainsCrossResourceEndpointReference(resource, reference)),
            _ => false
        };
    }

    private static bool IsCrossResourceEndpointReference(IResource resource, EndpointReference endpointReference)
        => !IsSameResource(resource, endpointReference.Resource);

    private static bool ContainsUnsupportedResourceReference(IResource resource, object? value)
    {
        // Vercel only knows how to turn endpoint references into deterministic production
        // aliases. Other resource references can represent connection strings, parameters,
        // or custom values that need a target-native mechanism this preview does not have.
        return value switch
        {
            null => false,
            string => false,
            ParameterResource => false,
            IResourceBuilder<ParameterResource> => false,
            EndpointReference => false,
            EndpointReferenceExpression => false,
            IResource referencedResource => !IsSameResource(resource, referencedResource),
            IValueWithReferences valueWithReferences => valueWithReferences.References.Any(reference => ContainsUnsupportedResourceReference(resource, reference)),
            IResourceBuilder<IResource> resourceBuilder => !IsSameResource(resource, resourceBuilder.Resource),
            _ => false
        };
    }

    private static bool IsSameResource(IResource resource, IResource otherResource)
        // Compare by Aspire resource name rather than object identity. Polyglot/ATS flows
        // can recreate resource references across an RPC boundary, but resource name is the
        // app-model identity used by deployment target maps.
        => string.Equals(resource.Name, otherResource.Name, StringComparison.Ordinal);

    private static bool IsHttpEndpoint(EndpointAnnotation endpoint)
        // URI scheme alone is not enough: Aspire can model a TCP transport with an HTTP
        // URI scheme. Vercel's container ingress here is HTTP-family traffic over TCP.
        => endpoint.Protocol == ProtocolType.Tcp
            && (string.Equals(endpoint.UriScheme, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.UriScheme, "https", StringComparison.OrdinalIgnoreCase))
            && (string.Equals(endpoint.Transport, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(endpoint.Transport, "http2", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, VercelDeploymentEntry> GetDeploymentEntriesByResourceName(IReadOnlyList<VercelDeploymentEntry> entries)
        => entries.ToDictionary(static entry => entry.Resource.Name, StringComparer.Ordinal);

    internal static IEnumerable<VercelDeploymentEntry> GetDeploymentEntries(DistributedApplicationModel model, VercelEnvironmentResource environment)
    {
        var computeEnvironments = model.Resources.OfType<IComputeEnvironmentResource>().Take(2).ToArray();
        // Match Aspire's single-environment convention only when Vercel is the sole compute
        // environment. With mixed targets, implicit selection would accidentally deploy
        // untargeted resources to every environment.
        bool allowImplicitTargeting = computeEnvironments.Length == 1 && ReferenceEquals(computeEnvironments[0], environment);

        foreach (var resource in model.Resources.OfType<IComputeResource>())
        {
            if (!IsTargetedToEnvironment(resource, environment, allowImplicitTargeting))
            {
                continue;
            }

            if (resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile))
            {
                yield return new(resource, dockerfile.ContextPath, dockerfile.DockerfilePath, dockerfile);
                continue;
            }

            if (resource is ProjectResource project)
            {
                string projectPath = project.GetProjectMetadata().ProjectPath;
                string sourceRoot = Path.GetDirectoryName(projectPath)
                    ?? throw new DistributedApplicationException($"Project resource '{resource.Name}' has project path '{projectPath}' without a containing directory.");
                yield return new(resource, sourceRoot);
                continue;
            }

            throw new DistributedApplicationException($"Resource '{resource.Name}' targets Vercel but is not an Aspire image build resource. Use a .NET project, a workload integration that publishes Dockerfile metadata, call PublishAsDockerFile, or configure the resource with WithDockerfile, WithDockerfileFactory, or WithDockerfileBuilder.");
        }
    }

    private static bool IsTargetedToEnvironment(IResource resource, VercelEnvironmentResource environment, bool allowImplicitTargeting)
    {
        var computeEnvironment = resource.GetComputeEnvironment();
        // Match Aspire's single-environment convention: when Vercel is the only compute
        // environment, image-build workloads implicitly target it.
        return ReferenceEquals(computeEnvironment, environment)
            || (computeEnvironment is null && allowImplicitTargeting);
    }

    private static void ValidateEntries(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        // Keep unsupported Vercel-preview cases in validation so publish/prereq/deploy fail
        // before mutating provider projects. Each failure names the Aspire concept that
        // cannot be projected rather than letting a later Vercel CLI call fail opaquely.
        if (entries.Count == 0)
        {
            throw new DistributedApplicationException("No image-build compute resources target Vercel. Add a .NET project, a workload with Aspire Dockerfile publish metadata, or use WithComputeEnvironment to target Vercel when multiple compute environments are present.");
        }

        foreach (var entry in entries)
        {
            if (!Directory.Exists(entry.SourceRoot))
            {
                throw new DistributedApplicationException($"The Vercel source root '{entry.SourceRoot}' for resource '{entry.Resource.Name}' does not exist.");
            }

            if (entry.Dockerfile is { DockerfileFactory: null } && !File.Exists(entry.DockerfilePath!))
            {
                throw new DistributedApplicationException($"The Vercel Dockerfile '{entry.DockerfilePath}' for resource '{entry.Resource.Name}' does not exist. Configure the resource with an existing Dockerfile or Aspire-generated Dockerfile metadata.");
            }

            ValidateUnsupportedResourceModel(entry);
        }

        ValidateUniqueVercelProjectNames(entries);
    }

    private static void ValidateUniqueVercelProjectNames(IReadOnlyList<VercelDeploymentEntry> entries)
    {
        // Production endpoint references use https://{projectName}.vercel.app. If two
        // resources resolve to the same Vercel project, endpoint references and destroy
        // ownership would both become ambiguous.
        var projectNames = entries
            .Select(entry => new
            {
                Entry = entry,
                ProjectLink = GetVercelProjectLink(entry),
                Linked = HasVercelProjectLinkFile(entry.SourceRoot)
            })
            .GroupBy(item => item.ProjectLink.ProjectName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToArray();

        if (projectNames.Length == 0)
        {
            return;
        }

        var collision = projectNames[0];
        string resources = string.Join(", ", collision.Select(static item => $"'{item.Entry.Resource.Name}'").Order(StringComparer.Ordinal));
        throw new DistributedApplicationException(
            $"Multiple Vercel resources resolve to project name '{collision.Key}' ({resources}). Vercel project names must be unique per environment because each resource deploys to and references one project production URL. Use WithVercelProjectName, distinct source directory names, or link each resource to a distinct Vercel project with .vercel/project.json.");
    }

    private static void ValidateUnsupportedResourceModel(VercelDeploymentEntry entry)
    {
        IResource resource = entry.Resource;

        // This method is intentionally conservative. Each rejected annotation has run-mode
        // or another deployment-target semantics that Vercel's Dockerfile deploy cannot
        // project without changing what the user modeled in the AppHost. If Vercel gains a
        // native equivalent later, add the mapping and tests here instead of silently ignoring it.
        if (resource.Annotations.OfType<ContainerRegistryReferenceAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire generic container registry/image push metadata, but Vercel deployments use Vercel Container Registry through a provider-owned prebuilt artifact. Remove WithContainerRegistry before deploying with the Aspire Vercel integration.");
        }

        if (resource.Annotations.OfType<ContainerMountAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire container volumes or bind mounts, but Vercel Dockerfile deployments do not support Aspire-managed container mounts. Move persistent state to a Vercel-supported external service or remove the mount.");
        }

        if (resource.Annotations.OfType<ContainerFilesSourceAnnotation>().Any()
            || resource.Annotations.OfType<ContainerFilesDestinationAnnotation>().Any()
            || resource.Annotations.OfType<ContainerFileSystemCallbackAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire container file mounts, but Vercel Dockerfile deployments build the checked-in source tree directly. Include required files in the source tree or deploy this resource outside the Aspire Vercel integration.");
        }

        if (resource.Annotations.OfType<ProbeAnnotation>().Any()
            || resource.Annotations.OfType<EndpointProbeAnnotation>().Any()
            || resource.Annotations.OfType<HealthCheckAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire health checks or container probes, but the Vercel preview integration does not map them to Vercel-native checks. Remove the Aspire probes or configure health behavior in Vercel.");
        }

        if (resource.Annotations.OfType<ReplicaAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire replicas or scale, but the Vercel preview integration does not map replica counts to Vercel-native scaling. Configure scaling in Vercel instead.");
        }

        if (resource.Annotations.OfType<WaitAnnotation>().Any())
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' configures Aspire wait/dependency ordering, but Vercel deploys each project independently and the preview integration does not preserve Aspire startup ordering. Remove the wait relationship or deploy dependent services separately.");
        }

        ValidateEndpointModel(entry);
        ValidateProjectName(entry);
    }

    private static void ValidateEndpointModel(VercelDeploymentEntry entry)
    {
        var endpoints = entry.Resource.Annotations.OfType<EndpointAnnotation>().ToArray();

        if (endpoints.Length == 0)
        {
            return;
        }

        // Reject the tempting Compose/ACA shapes up front: private listeners, multiple
        // target ports, and non-HTTP protocols do not have an equivalent in this preview's
        // single public Vercel container ingress.
        // Vercel's Dockerfile preview exposes one public platform ingress; it has no
        // Aspire-modeled private service network for internal endpoints.
        var internalEndpoint = endpoints.FirstOrDefault(static endpoint => !endpoint.IsExternal);
        if (internalEndpoint is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures endpoint '{internalEndpoint.Name}' as internal, but Vercel Dockerfile deployments expose public platform HTTPS ingress only. Mark the endpoint external or remove it before deploying to Vercel.");
        }

        var unsupportedEndpoint = endpoints.FirstOrDefault(static endpoint => !IsHttpEndpoint(endpoint));
        if (unsupportedEndpoint is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures endpoint '{unsupportedEndpoint.Name}' with scheme '{unsupportedEndpoint.UriScheme}' and transport '{unsupportedEndpoint.Transport}', but Vercel Dockerfile deployments support only HTTP or HTTPS endpoints with HTTP transports.");
        }

        var targetPorts = endpoints
            .Select(static endpoint => endpoint.TargetPort)
            .Where(static targetPort => targetPort.HasValue)
            .Select(static targetPort => targetPort!.Value)
            .Distinct()
            .ToArray();

        // Vercel provides one runtime listener through $PORT. Additional target ports would
        // look like ACA extra ports, but Vercel has no equivalent modeled here.
        if (targetPorts.Length > 1)
        {
            throw new DistributedApplicationException(
                $"Resource '{entry.Resource.Name}' configures multiple Aspire endpoint target ports, but Vercel Dockerfile deployments support only one HTTP listening port exposed through the $PORT environment variable.");
        }
    }

    private static void ValidateProjectName(VercelDeploymentEntry entry)
    {
        if (HasVercelProjectLinkFile(entry.SourceRoot))
        {
            return;
        }

        _ = GetVercelProjectName(entry);
    }

    private static async Task<VercelDeploymentEntry> PrepareDeploymentEntryAsync(PipelineStepContext context, VercelDeploymentEntry entry)
    {
        await ValidateVercelJsonAsync(entry.Resource, entry.SourceRoot, context.CancellationToken).ConfigureAwait(false);

        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string tempDirectory = outputService.GetTempDirectory(entry.Resource);
        string deployDirectory = Path.Combine(tempDirectory, "vercel-build-output");
        Directory.CreateDirectory(tempDirectory);
        if (Directory.Exists(deployDirectory))
        {
            Directory.Delete(deployDirectory, recursive: true);
        }

        // This is an output-only Build Output API root, not a staged copy of the source.
        // Aspire's built-in build/push steps read the real source/project directly; Vercel
        // deploy receives only generated metadata that points at the digest already pushed to VCR.
        Directory.CreateDirectory(deployDirectory);

        return entry with
        {
            TempDirectory = tempDirectory,
            DeployDirectory = deployDirectory
        };
    }

    private static async Task ValidateVercelJsonAsync(
        IResource resource,
        string sourceRoot,
        CancellationToken cancellationToken)
    {
        string vercelJsonPath = Path.Combine(sourceRoot, VercelJsonFileName);
        if (!File.Exists(vercelJsonPath))
        {
            return;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(vercelJsonPath, cancellationToken).ConfigureAwait(false)) as JsonObject
                ?? throw new DistributedApplicationException($"Resource '{resource.Name}' source root contains '{VercelJsonFileName}', but it is not a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException($"Resource '{resource.Name}' source root contains invalid '{VercelJsonFileName}'.", ex);
        }

        var unsupportedKey = VercelJsonBuildOutputUnsupportedKeys.FirstOrDefault(root.ContainsKey);
        if (unsupportedKey is not null)
        {
            throw new DistributedApplicationException(
                $"Resource '{resource.Name}' source root contains '{VercelJsonFileName}' with top-level '{unsupportedKey}', but the Aspire Vercel integration owns the generated Build Output API container function and catch-all routing configuration. Move that setting into the Dockerfile, AppHost environment variables, or Vercel project settings before deploying with the Aspire Vercel integration.");
        }
    }

    private static string GetDisplayDockerfilePath(VercelDeploymentEntry entry)
        => entry.Dockerfile is null
            ? "<project container>"
            : entry.Dockerfile.DockerfileFactory is null
                ? Path.GetRelativePath(entry.SourceRoot, entry.DockerfilePath!)
                : "<generated>";

    internal static string GetVercelProjectName(VercelDeploymentEntry entry)
        => GetVercelProjectLink(entry).ProjectName;

    internal static string GetVercelProjectName(IResource resource)
    {
        if (resource.TryGetLastAnnotation<DockerfileBuildAnnotation>(out var dockerfile))
        {
            return GetVercelProjectName(new VercelDeploymentEntry(resource, dockerfile.ContextPath, dockerfile.DockerfilePath, dockerfile));
        }

        if (resource is ProjectResource project)
        {
            string projectPath = project.GetProjectMetadata().ProjectPath;
            string sourceRoot = Path.GetDirectoryName(projectPath)
                ?? throw new DistributedApplicationException($"Project resource '{resource.Name}' has project path '{projectPath}' without a containing directory.");
            return GetVercelProjectName(new VercelDeploymentEntry(resource, sourceRoot));
        }

        throw new DistributedApplicationException($"Resource '{resource.Name}' targets Vercel but is not an Aspire image build resource. Use a .NET project, a workload integration that publishes Dockerfile metadata, call PublishAsDockerFile, or configure the resource with WithDockerfile, WithDockerfileFactory, or WithDockerfileBuilder.");
    }

    private static VercelProjectLink GetVercelProjectLink(VercelDeploymentEntry entry)
    {
        if (TryReadVercelProjectLink(entry.SourceRoot, out var projectLink))
        {
            return projectLink;
        }

        return new(GetManagedVercelProjectName(entry), ProjectId: null);
    }

    private static string GetVercelProjectOption(VercelDeploymentEntry entry)
    {
        var projectLink = GetVercelProjectLink(entry);
        return string.IsNullOrWhiteSpace(projectLink.ProjectId)
            ? projectLink.ProjectName
            : projectLink.ProjectId;
    }

    private static string GetManagedVercelProjectName(VercelDeploymentEntry entry)
    {
        if (entry.Resource.TryGetLastAnnotation<VercelProjectOptionsAnnotation>(out var options))
        {
            return options.ProjectName;
        }

        // The production endpoint contract is project-name based, so managed names must
        // be stable and Vercel-valid before deploy starts.
        string sourceRoot = Path.TrimEndingDirectorySeparator(entry.SourceRoot);
        string sourceRootName = Path.GetFileName(sourceRoot);

        if (TryCreateVercelProjectName(sourceRootName, out string? projectName)
            || TryCreateVercelProjectName(entry.Resource.Name, out projectName))
        {
            return projectName;
        }

        throw new DistributedApplicationException($"Could not infer a valid Vercel project name for resource '{entry.Resource.Name}' from source root '{entry.SourceRoot}'. Rename the source directory or link the source root to an existing Vercel project.");
    }

    internal static bool IsValidVercelProjectName(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName)
            || projectName.Length > VercelProjectNameMaxLength
            || !IsLowercaseAsciiLetterOrDigit(projectName[0])
            || !IsLowercaseAsciiLetterOrDigit(projectName[^1]))
        {
            return false;
        }

        return projectName.All(static character =>
            IsLowercaseAsciiLetterOrDigit(character)
            || character == '-');
    }

    private static bool TryCreateVercelProjectName(string? value, [NotNullWhen(true)] out string? projectName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            projectName = null;
            return false;
        }

        var builder = new StringBuilder(value.Length);
        bool previousWasSeparator = false;

        foreach (char character in value)
        {
            if (IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        projectName = builder
            .ToString()
            .Trim('-');

        if (projectName.Length > VercelProjectNameMaxLength)
        {
            projectName = projectName[..VercelProjectNameMaxLength].Trim('-');
        }

        if (projectName.Length == 0)
        {
            projectName = null;
            return false;
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(char character)
        => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool IsLowercaseAsciiLetterOrDigit(char character)
        => character is >= 'a' and <= 'z' or >= '0' and <= '9';

    private static bool HasVercelProjectLinkFile(string sourceRoot)
        => File.Exists(GetVercelProjectJsonPath(sourceRoot));

    private static bool TryReadVercelProjectLink(string sourceRoot, [NotNullWhen(true)] out VercelProjectLink? projectLink)
    {
        string projectJsonPath = GetVercelProjectJsonPath(sourceRoot);

        if (File.Exists(projectJsonPath))
        {
            // Vercel CLI writes linked project identity as:
            //   .vercel/project.json: { "projectId": "...", "orgId": "...", "projectName": "..." }
            // Treat it as user/provider ownership metadata rather than regenerating a managed
            // name. Destroy preserves these linked projects and only removes tracked env vars.
            using var document = JsonDocument.Parse(File.ReadAllText(projectJsonPath));
            string? projectName = GetJsonStringProperty(document.RootElement, "projectName");

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                projectLink = new(projectName, GetJsonStringProperty(document.RootElement, "projectId"));
                return true;
            }
        }

        projectLink = null;
        return false;
    }

    private static string? GetJsonStringProperty(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString())
                ? property.GetString()
                : null;

    internal static bool ProjectListContainsProject(string standardOutput, string projectName)
    {
        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            foreach (var project in EnumerateJsonArrayOrNamedArray(document.RootElement, "projects"))
            {
                if (string.Equals(GetJsonStringProperty(project, "name"), projectName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException("Failed to parse JSON output from 'vercel project ls'.", ex);
        }
    }

    internal static bool EnvironmentVariableListContainsName(string standardOutput, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            foreach (var environmentVariable in EnumerateJsonArrayOrNamedArray(document.RootElement, "envs"))
            {
                if (string.Equals(GetJsonStringProperty(environmentVariable, "key"), name, StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(GetJsonStringProperty(environmentVariable, "gitBranch")))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException("Failed to parse JSON output from 'vercel env ls'.", ex);
        }
    }

    private static JsonElement.ArrayEnumerator EnumerateJsonArrayOrNamedArray(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return array.EnumerateArray();
        }

        throw new JsonException($"Expected JSON array or object property '{propertyName}'.");
    }

    private static string GetVercelProjectJsonPath(string sourceRoot)
        => Path.Combine(sourceRoot, ".vercel", "project.json");

    internal static string GetDeploymentUrl(string standardOutput)
        => GetDeploymentResult(standardOutput).DeploymentUrl;

    internal static VercelDeploymentResult GetDeploymentResult(string standardOutput)
    {
        // `vercel deploy` output has changed between CLI versions and flags. Prefer structured
        // JSON when present, then fall back to the last plain HTTP(S) URL printed by the CLI.
        if (TryGetJsonDeploymentResult(standardOutput) is { } jsonDeploymentResult)
        {
            return jsonDeploymentResult;
        }

        // Older CLI versions printed the deployment URL as plain text. Keep the fallback so
        // we fail only when no usable URL exists, not because formatting changed slightly.
        string[] lines = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string? deploymentUrl = lines.LastOrDefault(IsHttpUrl);
        if (deploymentUrl is null)
        {
            throw new DistributedApplicationException($"Vercel deploy output did not contain an HTTP or HTTPS deployment URL. Output: {GetTrimmedOutput(standardOutput)}");
        }

        return new(DeploymentId: null, deploymentUrl);
    }

    internal static VercelDeploymentInspection GetDeploymentInspection(string standardOutput)
    {
        try
        {
            // Parse the Vercel inspect JSON shapes observed across CLI versions:
            //   { "readyState": "READY" }
            //   { "state": "READY" }
            //   { "deployment": { "readyState": "READY" } }
            //   { "deployment": { "state": "READY" } }
            using var document = JsonDocument.Parse(standardOutput);
            var root = document.RootElement;
            string? readyState = GetJsonStringProperty(root, "readyState")
                ?? GetJsonStringProperty(root, "state")
                ?? (root.TryGetProperty("deployment", out var deployment) ? GetJsonStringProperty(deployment, "readyState") : null)
                ?? (root.TryGetProperty("deployment", out deployment) ? GetJsonStringProperty(deployment, "state") : null);

            return new(readyState);
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException("Failed to parse JSON output from 'vercel inspect'.", ex);
        }
    }

    private static VercelDeploymentResult? TryGetJsonDeploymentResult(string standardOutput)
    {
        if (!standardOutput.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            var root = document.RootElement;

            if (TryGetDeploymentResult(root, out var deploymentResult))
            {
                return deploymentResult;
            }
        }
        catch (JsonException)
        {
            // Older Vercel CLI output is plain text; fall back to line-based URL extraction.
        }

        return null;
    }

    private static bool TryGetDeploymentResult(JsonElement root, [NotNullWhen(true)] out VercelDeploymentResult? deploymentResult)
    {
        // Parse the Vercel deploy JSON shapes observed from different CLI versions:
        //   { "deployment": { "url": "https://...", "id": "..." } }
        //   { "url": "https://...", "id": "..." }
        // Callers fall back to line-based extraction when deploy output is plain text.
        if (root.TryGetProperty("deployment", out var deployment)
            && deployment.TryGetProperty("url", out var nestedUrl)
            && TryGetHttpUrl(nestedUrl, out var nestedDeploymentUrl))
        {
            string? deploymentId = deployment.TryGetProperty("id", out var nestedId) && nestedId.ValueKind == JsonValueKind.String
                ? nestedId.GetString()
                : null;

            deploymentResult = new(deploymentId, nestedDeploymentUrl);
            return true;
        }

        if (root.TryGetProperty("url", out var url)
            && TryGetHttpUrl(url, out var rootDeploymentUrl))
        {
            string? deploymentId = root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : null;

            deploymentResult = new(deploymentId, rootDeploymentUrl);
            return true;
        }

        deploymentResult = null;
        return false;
    }

    private static bool TryGetHttpUrl(JsonElement urlElement, [NotNullWhen(true)] out string? url)
    {
        url = urlElement.ValueKind == JsonValueKind.String
            ? urlElement.GetString()
            : null;

        return IsHttpUrl(url);
    }

    private static bool IsHttpUrl([NotNullWhen(true)] string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";

    internal static bool TryGetVercelCliVersion(string output, [NotNullWhen(true)] out Version? version)
    {
        // The CLI can print banners/warnings around the version. Extract the first semantic
        // x.y.z token instead of requiring a line to be exactly the version string.
        var match = Regex.Match(output, @"(?<!\d)(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success)
        {
            version = null;
            return false;
        }

        version = new(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    private static string GetTrimmedOutput(string output)
        => string.IsNullOrWhiteSpace(output) ? "<empty>" : output.Trim();

    private static DistributedApplicationException CreateCliException(string operation, string cliPath, VercelCliResult result)
    {
        string output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();

        return new DistributedApplicationException($"Failed to {operation} using '{cliPath}' (exit code {result.ExitCode}). {output}");
    }
}

internal sealed record VercelDeploymentEntry(
    IResource Resource,
    string SourceRoot,
    string? DockerfilePath = null,
    DockerfileBuildAnnotation? Dockerfile = null,
    string TempDirectory = "",
    string DeployDirectory = "");

internal sealed record VercelDeploymentPlan(string Environment, VercelDeploymentPlanEntry[] Deployments);

internal sealed record VercelDeploymentPlanEntry(string ResourceName, string DockerfilePath, string DeployCommand, string[] EnvironmentVariables);

internal sealed record VercelEnvironmentConfiguration(
    IReadOnlyList<KeyValuePair<string, string>> DeploymentEnvironmentVariables,
    IReadOnlyList<KeyValuePair<string, string>> ProjectEnvironmentVariables)
{
    public static VercelEnvironmentConfiguration Empty { get; } = new([], []);

    public IEnumerable<string> AllEnvironmentVariableNames =>
        DeploymentEnvironmentVariables.Select(static variable => variable.Key)
            .Concat(ProjectEnvironmentVariables.Select(static variable => variable.Key));
}

internal sealed record VercelDeploymentResult(string? DeploymentId, string DeploymentUrl);

internal sealed record VercelDeploymentInspection(string? ReadyState);

internal sealed record PreviousVercelDeployment(VercelDeploymentStateEntry Entry, string ProjectEnvironment);

internal sealed record VercelDeploymentState(
    int SchemaVersion,
    string Environment,
    string? Scope,
    string? Target,
    bool Production,
    VercelDeploymentStateEntry[] Deployments);

internal sealed record VercelDeploymentStateEntry(
    string ResourceName,
    string ProjectName,
    string? ProjectId,
    string? DeploymentId,
    string? DeploymentUrl,
    string SourceRoot,
    bool ManagedByAspire)
{
    public string? ProductionUrl { get; init; }

    public string? VcrImageDigest { get; init; }

    public int? BuildOutputApiVersion { get; init; }

    public string[] ProjectEnvironmentVariables { get; init; } = [];
}

internal sealed record VercelImageReference(string Reference, string Digest);

internal sealed record VercelPreparedDeploymentAnnotation(
    VercelDeploymentEntry Entry,
    VercelProjectLink ProjectLink,
    VercelPulledProjectContext ProjectContext,
    bool ManagedByAspire,
    string RemoteImageName,
    string RemoteImageTag,
    string TaggedImageReference) : IResourceAnnotation;

internal sealed class VercelImagePushOptionsCallbackAnnotation : IResourceAnnotation
{
}

internal sealed record VercelProjectLink(string ProjectName, string? ProjectId);

internal sealed record VercelPulledProject(
    string ProjectName,
    string? ProjectId,
    string? OrgId,
    string ProjectJsonContent,
    string OidcToken);

internal sealed record VercelPulledProjectContext(
    VercelEnvironmentConfiguration EnvironmentConfiguration,
    VercelPulledProject PulledProject,
    VercelOidcClaims OidcClaims);

internal sealed record VercelPulledProjectSettings(string ProjectName, string? ProjectId, string? OrgId);

internal sealed record VercelOidcClaims(string? OwnerId, string? Owner, string? Project, string? ProjectId);

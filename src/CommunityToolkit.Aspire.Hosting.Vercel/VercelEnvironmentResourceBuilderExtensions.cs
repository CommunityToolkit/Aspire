using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using CommunityToolkit.Aspire.Hosting.Vercel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Provides the public AppHost entry points for adding a Vercel compute environment and choosing
/// how it maps to Vercel scopes, production deployments, and custom targets.
/// </summary>
[Experimental("CTASPIREVERCEL001")]
public static class VercelEnvironmentResourceBuilderExtensions
{
    /// <summary>
    /// Adds a publish/deploy-only Vercel environment resource.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The Aspire resource name for the Vercel environment.</param>
    /// <returns>A resource builder for the Vercel environment.</returns>
    /// <remarks>
    /// The Vercel environment is not added to the local run model. During publish and deploy it validates image-build resources and,
    /// during deploy, invokes the Vercel CLI using the current login or <c>VERCEL_TOKEN</c>.
    /// See <see href="https://vercel.com/docs/cli">Vercel CLI documentation</see>.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<VercelEnvironmentResource> AddVercelEnvironment(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.Services.TryAddSingleton<IVercelCliRunner, VercelCliRunner>();
        builder.Services.TryAddSingleton<IVercelContainerRegistryClient, VercelContainerRegistryClient>();
        builder.Services.DecorateVercelContainerImageManager();

        var resource = new VercelEnvironmentResource(name);
        var resourceBuilder = builder.ExecutionContext.IsRunMode
            ? builder.CreateResourceBuilder(resource)
            : builder.AddResource(resource);

        // The actions are plain delegates over injectable services (CLI runner, registry
        // client, state/output managers). Tests materialize the pipeline steps and invoke
        // these delegates directly so publish/prereq/deploy/destroy logic does not require a
        // live Aspire CLI pipeline process.
        return resourceBuilder
            .WithAnnotation(new VercelEnvironmentOptionsAnnotation(), ResourceAnnotationMutationBehavior.Replace)
            .WithPipelineStepFactory(_ =>
            [
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.PublishStepNamePrefix}{resource.Name}",
                    Description = $"Generate the Vercel Build Output API plan for '{resource.Name}'.",
                    Resource = resource,
                    DependsOnSteps = [WellKnownPipelineSteps.ValidateComputeEnvironments],
                    RequiredBySteps = [WellKnownPipelineSteps.Publish, WellKnownPipelineSteps.Deploy],
                    Action = context => VercelDeploymentStep.WriteDeploymentPlanAsync(context, resource)
                },
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.DeployPrereqStepNamePrefix}{resource.Name}",
                    Description = $"Create/link Vercel projects and configure VCR image pushes for '{resource.Name}'.",
                    Resource = resource,
                    DependsOnSteps = [WellKnownPipelineSteps.ValidateComputeEnvironments],
                    RequiredBySteps = [WellKnownPipelineSteps.Deploy],
                    Action = context => VercelDeploymentStep.ValidatePrerequisitesAsync(context, resource)
                },
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.DeployStepNamePrefix}{resource.Name}",
                    Description = $"Deploy the digest-pinned VCR images to Vercel with --prebuilt for '{resource.Name}'.",
                    Resource = resource,
                    Tags = ["vercel-deploy"],
                    DependsOnSteps = [$"{VercelDeploymentStep.DeployPrereqStepNamePrefix}{resource.Name}"],
                    RequiredBySteps = [WellKnownPipelineSteps.Deploy],
                    Action = context => VercelDeploymentStep.DeployAsync(context, resource)
                },
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.DestroyStepNamePrefix}{resource.Name}",
                    Description = $"Destroy Vercel resources for environment '{resource.Name}'.",
                    Resource = resource,
                    RequiredBySteps = [WellKnownPipelineSteps.Destroy],
                    Action = context => VercelDeploymentStep.DestroyAsync(context, resource)
                }
            ])
            .WithAnnotation(new PipelineConfigurationAnnotation(context => VercelDeploymentStep.ConfigurePipeline(context, resource)));
    }

    /// <summary>
    /// Configures the Vercel team or account scope for deployments.
    /// </summary>
    /// <param name="builder">The Vercel environment builder.</param>
    /// <param name="scope">The Vercel scope passed to <c>vercel deploy --scope</c>.</param>
    /// <returns>The Vercel environment builder.</returns>
    [AspireExport]
    public static IResourceBuilder<VercelEnvironmentResource> WithVercelScope(
        this IResourceBuilder<VercelEnvironmentResource> builder,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        return builder.WithVercelOptions(options => options with { Scope = scope });
    }

    /// <summary>
    /// Configures deployments to target Vercel production.
    /// </summary>
    /// <param name="builder">The Vercel environment builder.</param>
    /// <returns>The Vercel environment builder.</returns>
    /// <remarks>
    /// This adds <c>--prod</c> to Vercel CLI deployments and clears any custom target configured with <see cref="WithVercelTarget"/>.
    /// Production deployments have deterministic <c>https://{project}.vercel.app</c> aliases that Aspire endpoint references can use.
    /// See <see href="https://vercel.com/docs/cli/deploy">Vercel deploy CLI documentation</see>.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<VercelEnvironmentResource> WithVercelProductionDeployments(
        this IResourceBuilder<VercelEnvironmentResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVercelOptions(options => options with { Production = true, Target = null });
    }

    /// <summary>
    /// Configures deployments to use a Vercel target environment.
    /// </summary>
    /// <param name="builder">The Vercel environment builder.</param>
    /// <param name="target">The target value passed to <c>vercel deploy --target</c>, such as <c>preview</c> or a custom environment.</param>
    /// <returns>The Vercel environment builder.</returns>
    /// <remarks>
    /// This clears production deployment mode configured with <see cref="WithVercelProductionDeployments"/>.
    /// Preview and custom-target URLs are assigned after deployment, so Aspire endpoint references are unavailable for these targets.
    /// See <see href="https://vercel.com/docs/deployments/generated-urls">Vercel generated URL documentation</see>.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<VercelEnvironmentResource> WithVercelTarget(
        this IResourceBuilder<VercelEnvironmentResource> builder,
        string target)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        return builder.WithVercelOptions(options => options with { Production = false, Target = target });
    }

    private static IResourceBuilder<VercelEnvironmentResource> WithVercelOptions(
        this IResourceBuilder<VercelEnvironmentResource> builder,
        Func<VercelEnvironmentOptionsAnnotation, VercelEnvironmentOptionsAnnotation> configure)
    {
        var current = builder.Resource.GetVercelOptions();
        var updated = configure(current);

        return builder.WithAnnotation(updated, ResourceAnnotationMutationBehavior.Replace);
    }

    private static void DecorateVercelContainerImageManager(this IServiceCollection services)
    {
        // Aspire owns image build/push. Vercel only needs to re-authenticate project-scoped
        // VCR credentials immediately before Vercel-targeted pushes. Decorate the existing
        // image manager once and leave all non-Vercel resources on the original path.
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(VercelContainerImageManagerDecorationMarker)))
        {
            return;
        }

        int descriptorIndex = -1;
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IResourceContainerImageManager))
            {
                descriptorIndex = i;
                break;
            }
        }

        if (descriptorIndex < 0)
        {
            return;
        }

        var descriptor = services[descriptorIndex];
        services.RemoveAt(descriptorIndex);
        services.Insert(
            descriptorIndex,
            ServiceDescriptor.Describe(
                typeof(IResourceContainerImageManager),
                serviceProvider =>
                {
                    var inner = (IResourceContainerImageManager)CreateService(serviceProvider, descriptor);
                    return ActivatorUtilities.CreateInstance<VercelResourceContainerImageManager>(serviceProvider, inner);
                },
                descriptor.Lifetime));
        services.AddSingleton<VercelContainerImageManagerDecorationMarker>();
    }

    private static object CreateService(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider)!;
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException($"The {nameof(IResourceContainerImageManager)} service registration is missing an implementation.");
    }

    private sealed class VercelContainerImageManagerDecorationMarker;

}

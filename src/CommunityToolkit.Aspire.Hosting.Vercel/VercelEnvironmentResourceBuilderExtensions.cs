using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Vercel;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for deploying Aspire workloads to Vercel Dockerfile hosting.
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
    /// The Vercel environment is not added to the local run model. During publish and deploy it validates Dockerfile build metadata for
    /// resources and, during deploy, invokes the Vercel CLI using the current login or <c>VERCEL_TOKEN</c>.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<VercelEnvironmentResource> AddVercelEnvironment(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.Services.TryAddSingleton<IVercelCliRunner, VercelCliRunner>();

        var resource = new VercelEnvironmentResource(name);
        var resourceBuilder = builder.ExecutionContext.IsRunMode
            ? builder.CreateResourceBuilder(resource)
            : builder.AddResource(resource);

        return resourceBuilder
            .WithAnnotation(new VercelEnvironmentOptionsAnnotation(), ResourceAnnotationMutationBehavior.Replace)
            .WithPipelineStepFactory(_ =>
            [
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.PublishStepNamePrefix}{resource.Name}",
                    Description = $"Generate Vercel deployment plan for '{resource.Name}'.",
                    Resource = resource,
                    DependsOnSteps = [WellKnownPipelineSteps.ValidateComputeEnvironments],
                    RequiredBySteps = [WellKnownPipelineSteps.Publish, WellKnownPipelineSteps.Deploy],
                    Action = context => VercelDeploymentStep.WriteDeploymentPlanAsync(context, resource)
                },
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.DeployPrereqStepNamePrefix}{resource.Name}",
                    Description = $"Validate Vercel CLI prerequisites for '{resource.Name}'.",
                    Resource = resource,
                    DependsOnSteps = [WellKnownPipelineSteps.ValidateComputeEnvironments],
                    RequiredBySteps = [WellKnownPipelineSteps.Deploy],
                    Action = context => VercelDeploymentStep.ValidatePrerequisitesAsync(context, resource)
                },
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.DeployStepNamePrefix}{resource.Name}",
                    Description = $"Deploy resources to Vercel environment '{resource.Name}'.",
                    Resource = resource,
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
            ]);
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
    /// <remarks>This adds <c>--prod</c> to Vercel CLI deployments and clears any custom target configured with <see cref="WithVercelTarget"/>.</remarks>
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
    /// <remarks>This clears production deployment mode configured with <see cref="WithVercelProductionDeployments"/>.</remarks>
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

}

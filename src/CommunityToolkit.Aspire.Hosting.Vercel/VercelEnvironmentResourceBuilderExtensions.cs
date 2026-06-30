using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Vercel;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for deploying Dockerfile-based resources to Vercel.
/// </summary>
[Experimental("CTASPIREVERCEL001")]
public static class VercelEnvironmentResourceBuilderExtensions
{
    private const string DefaultDockerfilePath = "Dockerfile.vercel";

    /// <summary>
    /// Adds a publish/deploy-only Vercel environment resource.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The Aspire resource name for the Vercel environment.</param>
    /// <returns>A resource builder for the Vercel environment.</returns>
    /// <remarks>
    /// The Vercel environment is not added to the local run model. During publish and deploy it validates Dockerfile-based
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
                    RequiredBySteps = [WellKnownPipelineSteps.Publish, WellKnownPipelineSteps.Deploy],
                    Action = context => VercelDeploymentStep.WriteDeploymentPlanAsync(context, resource)
                },
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.DeployPrereqStepNamePrefix}{resource.Name}",
                    Description = $"Validate Vercel CLI prerequisites for '{resource.Name}'.",
                    Resource = resource,
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
                    Name = $"{VercelDeploymentStep.DestroyPrereqStepNamePrefix}{resource.Name}",
                    Description = $"Validate Vercel CLI prerequisites for destroying '{resource.Name}'.",
                    Resource = resource,
                    RequiredBySteps = [WellKnownPipelineSteps.Destroy],
                    Action = context => VercelDeploymentStep.ValidateCliPrerequisitesAsync(context, resource)
                },
                new PipelineStep
                {
                    Name = $"{VercelDeploymentStep.DestroyStepNamePrefix}{resource.Name}",
                    Description = $"Destroy Vercel resources for environment '{resource.Name}'.",
                    Resource = resource,
                    DependsOnSteps = [$"{VercelDeploymentStep.DestroyPrereqStepNamePrefix}{resource.Name}"],
                    RequiredBySteps = [WellKnownPipelineSteps.Destroy],
                    Action = context => VercelDeploymentStep.DestroyAsync(context, resource)
                }
            ]);
    }

    /// <summary>
    /// Configures the Vercel CLI executable path used by deploy and destroy pipeline steps.
    /// </summary>
    /// <param name="builder">The Vercel environment builder.</param>
    /// <param name="cliPath">The executable name or absolute path for the Vercel CLI.</param>
    /// <returns>The Vercel environment builder.</returns>
    [AspireExport]
    public static IResourceBuilder<VercelEnvironmentResource> WithVercelCliPath(
        this IResourceBuilder<VercelEnvironmentResource> builder,
        string cliPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(cliPath);

        return builder.WithVercelOptions(options => options with { CliPath = cliPath });
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

    /// <summary>
    /// Configures a .NET project resource to deploy to Vercel using a Dockerfile at the project root.
    /// </summary>
    /// <param name="builder">The project resource builder.</param>
    /// <param name="environment">The Vercel environment builder.</param>
    /// <param name="dockerfilePath">The Dockerfile path relative to the project root. Defaults to <c>Dockerfile.vercel</c>.</param>
    /// <returns>The project resource builder.</returns>
    [AspireExport("publishProjectAsVercel", MethodName = "publishAsVercel")]
    public static IResourceBuilder<ProjectResource> PublishAsVercel(
        this IResourceBuilder<ProjectResource> builder,
        IResourceBuilder<VercelEnvironmentResource> environment,
        string? dockerfilePath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(environment);

        if (!builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        return builder.WithVercelDeployment(environment, sourceRoot: null, dockerfilePath);
    }

    /// <summary>
    /// Configures an executable resource to deploy to Vercel using a Dockerfile at the executable working directory.
    /// </summary>
    /// <param name="builder">The executable resource builder.</param>
    /// <param name="environment">The Vercel environment builder.</param>
    /// <param name="dockerfilePath">The Dockerfile path relative to the executable working directory. Defaults to <c>Dockerfile.vercel</c>.</param>
    /// <returns>The executable resource builder.</returns>
    [AspireExport("publishExecutableAsVercel", MethodName = "publishAsVercel")]
    public static IResourceBuilder<ExecutableResource> PublishAsVercel(
        this IResourceBuilder<ExecutableResource> builder,
        IResourceBuilder<VercelEnvironmentResource> environment,
        string? dockerfilePath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(environment);

        if (!builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        return builder.WithVercelDeployment(environment, sourceRoot: null, dockerfilePath);
    }

    /// <summary>
    /// Configures a container resource to deploy to Vercel using a Dockerfile-based source root.
    /// </summary>
    /// <param name="builder">The container resource builder.</param>
    /// <param name="environment">The Vercel environment builder.</param>
    /// <param name="sourceRoot">The source root passed to <c>vercel deploy --cwd</c>. Defaults to the container Dockerfile build context.</param>
    /// <param name="dockerfilePath">The Dockerfile path relative to the source root. Defaults to <c>Dockerfile.vercel</c>.</param>
    /// <returns>The container resource builder.</returns>
    [AspireExport("publishContainerAsVercel", MethodName = "publishAsVercel")]
    public static IResourceBuilder<ContainerResource> PublishAsVercel(
        this IResourceBuilder<ContainerResource> builder,
        IResourceBuilder<VercelEnvironmentResource> environment,
        string? sourceRoot = null,
        string? dockerfilePath = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(environment);

        if (!builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder;
        }

        return builder.WithVercelDeployment(environment, sourceRoot, dockerfilePath);
    }

    private static IResourceBuilder<T> WithVercelDeployment<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<VercelEnvironmentResource> environment,
        string? sourceRoot,
        string? dockerfilePath)
        where T : IComputeResource
    {
        string? normalizedSourceRoot = sourceRoot is null
            ? null
            : ResolvePath(builder.ApplicationBuilder, sourceRoot);

        string normalizedDockerfilePath = string.IsNullOrWhiteSpace(dockerfilePath)
            ? DefaultDockerfilePath
            : dockerfilePath;

        return builder
            .WithComputeEnvironment(environment)
            .WithAnnotation(new VercelDeploymentAnnotation(normalizedSourceRoot, normalizedDockerfilePath), ResourceAnnotationMutationBehavior.Replace);
    }

    private static IResourceBuilder<VercelEnvironmentResource> WithVercelOptions(
        this IResourceBuilder<VercelEnvironmentResource> builder,
        Func<VercelEnvironmentOptionsAnnotation, VercelEnvironmentOptionsAnnotation> configure)
    {
        var current = builder.Resource.GetVercelOptions();
        var updated = configure(current);

        return builder.WithAnnotation(updated, ResourceAnnotationMutationBehavior.Replace);
    }

    private static string ResolvePath(IDistributedApplicationBuilder builder, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(builder.AppHostDirectory, path));
}

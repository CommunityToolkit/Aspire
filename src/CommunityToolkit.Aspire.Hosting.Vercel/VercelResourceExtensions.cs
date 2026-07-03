#pragma warning disable ASPIREPIPELINES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelResourceExtensions
{
    [AspireExportIgnore(Reason = "Internal Vercel annotation access is not part of the generated AppHost API.")]
    public static VercelEnvironmentOptionsAnnotation GetVercelOptions(this VercelEnvironmentResource resource)
    {
        if (resource.TryGetLastAnnotation<VercelEnvironmentOptionsAnnotation>(out var options))
        {
            return options;
        }

        return new VercelEnvironmentOptionsAnnotation();
    }

    [AspireExportIgnore(Reason = "Internal Vercel pipeline cleanup is not part of the generated AppHost API.")]
    public static IResourceBuilder<TResource> WithVercelPipelineFinalizer<TResource>(this IResourceBuilder<TResource> builder)
        where TResource : IResource
    {
        if (builder.Resource.Annotations.OfType<VercelPipelineFinalizerAnnotation>().Any())
        {
            return builder;
        }

        builder.Resource.Annotations.Add(new VercelPipelineFinalizerAnnotation());
        builder.Resource.Annotations.Add(new PipelineConfigurationAnnotation(VercelDeploymentStep.NormalizePipelineDependencies));
        return builder;
    }
}

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Vercel;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring Aspire workloads deployed to Vercel Dockerfile hosting.
/// </summary>
[Experimental("CTASPIREVERCEL001")]
public static class VercelResourceBuilderExtensions
{
    /// <summary>
    /// Configures the Vercel project name to use when deploying an Aspire-managed Vercel project for the resource.
    /// </summary>
    /// <typeparam name="TResource">The compute resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="projectName">The Vercel project name. Use lowercase letters, digits, and hyphens.</param>
    /// <returns>The resource builder.</returns>
    /// <remarks>
    /// This setting is used only when the source root is not already linked to a Vercel project with <c>.vercel/project.json</c>.
    /// Linked projects keep their existing provider project identity.
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<TResource> WithVercelProjectName<TResource>(
        this IResourceBuilder<TResource> builder,
        string projectName)
        where TResource : IComputeResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);

        if (!VercelDeploymentStep.IsValidVercelProjectName(projectName))
        {
            throw new ArgumentException(
                $"Vercel project name '{projectName}' is invalid. Use lowercase letters, digits, and hyphens; start and end with a letter or digit; and keep the name at most 100 characters.",
                nameof(projectName));
        }

        return builder.WithAnnotation(new VercelProjectOptionsAnnotation(projectName), ResourceAnnotationMutationBehavior.Replace);
    }
}

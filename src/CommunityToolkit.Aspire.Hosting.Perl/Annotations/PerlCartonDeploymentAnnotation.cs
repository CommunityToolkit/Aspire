using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Annotations;

/// <summary>
/// Records whether <c>carton install --deployment</c> should be used during publish-mode Dockerfile generation.
/// When <see langword="true"/>, Carton uses <c>--deployment</c> to install from a locked <c>cpanfile.snapshot</c>.
/// </summary>
/// <param name="useDeployment">Whether to pass <c>--deployment</c> to <c>carton install</c>.</param>
internal sealed class PerlCartonDeploymentAnnotation(bool useDeployment) : IResourceAnnotation
{
    /// <summary>
    /// Gets a value indicating whether <c>--deployment</c> should be used with <c>carton install</c>.
    /// </summary>
    public bool UseDeployment { get; } = useDeployment;
}

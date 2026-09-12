#pragma warning disable ASPIREPIPELINES003
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Decorates Aspire's container image manager to add the VCR login behavior needed before
/// pushing Vercel-targeted resources, while leaving unrelated resource pushes untouched.
/// </summary>
internal sealed class VercelResourceContainerImageManager(
    IResourceContainerImageManager inner,
    IVercelCliRunner runner) : IResourceContainerImageManager
{
    private readonly SemaphoreSlim _pushLock = new(1, 1);

    public Task BuildImageAsync(IResource resource, CancellationToken cancellationToken = default)
        => inner.BuildImageAsync(resource, cancellationToken);

    public Task BuildImagesAsync(IEnumerable<IResource> resources, CancellationToken cancellationToken = default)
        => inner.BuildImagesAsync(resources, cancellationToken);

    public async Task PushImageAsync(IResource resource, CancellationToken cancellationToken)
    {
        var preparedDeployment = resource.Annotations.OfType<VercelPreparedDeploymentAnnotation>().LastOrDefault();
        if (preparedDeployment is null)
        {
            await inner.PushImageAsync(resource, cancellationToken).ConfigureAwait(false);
            return;
        }

        // VCR OIDC tokens are scoped to a Vercel project, while Docker stores credentials
        // by registry host. Built-in Aspire push steps can run in parallel, so serialize
        // Vercel pushes and login immediately before each push to avoid cross-project token races.
        await _pushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await VercelDeploymentStep.LoginToVcrAsync(
                runner,
                preparedDeployment.ProjectContext.PulledProject.OidcToken,
                preparedDeployment.ProjectContext.OidcClaims,
                cancellationToken).ConfigureAwait(false);

            await inner.PushImageAsync(resource, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pushLock.Release();
        }
    }
}

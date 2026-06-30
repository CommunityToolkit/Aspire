#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

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
}

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal sealed record VercelEnvironmentOptionsAnnotation : IResourceAnnotation
{
    public string? Scope { get; init; }

    public string? Target { get; init; }

    public bool Production { get; init; }
}

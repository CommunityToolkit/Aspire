using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Stores Vercel environment-level options on the Aspire resource model so pipeline steps can
/// consistently apply scope, production, and custom-target behavior.
/// </summary>
internal sealed record VercelEnvironmentOptionsAnnotation : IResourceAnnotation
{
    public string? Scope { get; init; }

    public string? Target { get; init; }

    public bool Production { get; init; }
}

using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal sealed record VercelEnvironmentOptionsAnnotation : IResourceAnnotation
{
    public const string DefaultCliPath = "vercel";

    public string CliPath { get; init; } = DefaultCliPath;

    public string? Scope { get; init; }

    public string? Target { get; init; }

    public bool Production { get; init; }
}

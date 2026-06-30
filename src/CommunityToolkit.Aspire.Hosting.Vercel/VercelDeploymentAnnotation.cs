using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal sealed class VercelDeploymentAnnotation(string? sourceRoot, string dockerfilePath) : IResourceAnnotation
{
    public string? SourceRoot { get; } = sourceRoot;

    public string DockerfilePath { get; } = dockerfilePath;
}

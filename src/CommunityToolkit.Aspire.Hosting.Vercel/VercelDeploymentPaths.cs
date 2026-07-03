namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelDeploymentPaths
{
    public static string GetDeployDirectory(VercelDeploymentEntry entry)
        => string.IsNullOrWhiteSpace(entry.DeployDirectory) ? entry.SourceRoot : entry.DeployDirectory;
}

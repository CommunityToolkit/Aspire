namespace CommunityToolkit.Aspire.Hosting.Vercel;

/// <summary>
/// Centralizes Vercel protocol names and generated-file constants so Build Output, VCR, state,
/// and CLI components stay aligned as provider contracts evolve.
/// </summary>
internal static class VercelConstants
{
    internal const string DeploymentPlanFileName = "vercel-deployments.json";
    internal const string StateSectionNamePrefix = "communitytoolkit.vercel.";
    internal const int DeploymentStateSchemaVersion = 1;
    internal const int ProjectNameMaxLength = 100;
    internal const string CliFileName = "vercel";
    internal const string DockerCliFileName = "docker";
    internal const string VcrRegistry = "vcr.vercel.com";
    internal const string JsonFileName = "vercel.json";
    internal const string ProjectFileName = "project.json";
    internal const string DirectoryName = ".vercel";
    internal const string OutputDirectoryName = "output";
    internal const string OidcTokenEnvironmentVariable = "VERCEL_OIDC_TOKEN";
    internal const string ContainerServiceName = "app";
    internal const int BuildOutputApiVersion = 3;
}

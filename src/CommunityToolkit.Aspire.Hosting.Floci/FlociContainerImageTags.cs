namespace CommunityToolkit.Aspire.Hosting.Floci;

internal static class FlociContainerImageTags
{
    public const string Registry = "docker.io";
    public const string Image = "floci/floci";
    public const string Tag = "latest"; // Consider pinning to a specific version tag for reproducible builds/runs (or document why only `latest` is available).

    public const string UIRegistry = "docker.io";
    public const string UIImage = "floci/floci-ui";
    public const string UITag = "0.2.0";
}

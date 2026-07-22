namespace CommunityToolkit.Aspire.Hosting.Floci;

internal static class FlociContainerImageTags
{
    public const string AwsRegistry = "docker.io";
    public const string AwsImage = "floci/floci";
    public const string AwsTag = "latest"; // Consider pinning to a specific version tag for reproducible builds/runs (or document why only `latest` is available).

    public const string AzureRegistry = "docker.io";
    public const string AzureImage = "floci/floci-az";
    public const string AzureTag = "latest";

    public const string GcpRegistry = "docker.io";
    public const string GcpImage = "floci/floci-gcp";
    public const string GcpTag = "latest";

    public const string UIRegistry = "docker.io";
    public const string UIImage = "floci/floci-ui";
    public const string UITag = "0.2.0";
}

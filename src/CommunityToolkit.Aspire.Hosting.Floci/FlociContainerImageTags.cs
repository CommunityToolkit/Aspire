namespace CommunityToolkit.Aspire.Hosting.Floci;

internal static class FlociContainerImageTags
{
    public const string AwsRegistry = "docker.io";
    public const string AwsImage = "floci/floci";
    public const string AwsTag = "1.6.0";

    public const string AzureRegistry = "docker.io";
    public const string AzureImage = "floci/floci-az";
    public const string AzureTag = "0.10.0";

    public const string GcpRegistry = "docker.io";
    public const string GcpImage = "floci/floci-gcp";
    public const string GcpTag = "0.6.0";

    public const string UIRegistry = "docker.io";
    public const string UIImage = "floci/floci-ui";
    public const string UITag = "0.2.0";
}

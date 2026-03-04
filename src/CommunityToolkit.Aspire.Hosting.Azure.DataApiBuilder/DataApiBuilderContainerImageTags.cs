namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder;

/// <summary>
/// Container image tags for the Data API Builder container.
/// </summary>
internal static class DataApiBuilderContainerImageTags
{
    /// <summary>The container registry.</summary>
    public const string Registry = "mcr.microsoft.com";

    /// <summary>The container image name.</summary>
    public const string Image = "azure-databases/data-api-builder";

    /// <summary>The default container image tag.</summary>
    public const string Tag = "1.6.87";
}

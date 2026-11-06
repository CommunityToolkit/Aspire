namespace CommunityToolkit.Aspire.Hosting.Tempo;

/// <summary>
/// Defines the default Grafana Tempo container image.
/// </summary>
internal static class TempoContainerImageTags
{
    /// <summary>
    /// The container registry hosting the image.
    /// </summary>
    public const string Registry = "docker.io";

    /// <summary>
    /// The Grafana Tempo image name.
    /// </summary>
    public const string Image = "grafana/tempo";

    /// <summary>
    /// The default image version.
    /// </summary>
    public const string Tag = "3.1.1";
}
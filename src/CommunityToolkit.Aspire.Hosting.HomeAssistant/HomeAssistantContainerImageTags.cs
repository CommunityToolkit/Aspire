namespace Aspire.CommunityToolkit.Hosting.HomeAssistant;

/// <summary>
/// A static class that represents various aspect of the HomeAssistant container image, such as <c>registry/image:tag</c>.
/// </summary>
/// <remarks>
/// <inheritdoc cref="HomeAssistantContainerImageTags.Registry"/>/<inheritdoc cref="HomeAssistantContainerImageTags.Image"/>:<inheritdoc cref="HomeAssistantContainerImageTags.Tag"/>
/// </remarks>
internal static class HomeAssistantContainerImageTags
{
    /// <remarks>2024.12</remarks>
    public const string Tag = "2024.12";

    /// <remarks>docker.io</remarks>
    public const string Registry = "docker.io";

    /// <remarks>homeassistant/home-assistant</remarks>
    public const string Image = "homeassistant/home-assistant";
}

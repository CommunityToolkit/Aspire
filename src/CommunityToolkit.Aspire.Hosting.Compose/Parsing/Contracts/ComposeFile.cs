using YamlDotNet.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

/// <summary>
/// Represents a parsed Docker Compose file.
/// Supports all compose format versions: v1 (legacy), v2.x, v3.x, and modern Compose Spec.
/// </summary>
internal sealed class ComposeFile
{
    /// <summary>
    /// Gets or sets the compose file version (v2/v3). Optional in modern Compose Spec format.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.TopLevel.Version)]
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the services defined in the compose file.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.TopLevel.Services)]
    public Dictionary<string, ComposeService> Services { get; set; } = [];

    /// <summary>
    /// Gets or sets the named volumes defined in the compose file.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.TopLevel.Volumes)]
    public Dictionary<string, ComposeVolume?>? Volumes { get; set; }

    /// <summary>
    /// Gets or sets the networks defined in the compose file.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.TopLevel.Networks)]
    public Dictionary<string, ComposeNetwork?>? Networks { get; set; }
}

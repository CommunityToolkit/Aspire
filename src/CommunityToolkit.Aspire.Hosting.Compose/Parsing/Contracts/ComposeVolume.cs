using YamlDotNet.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

/// <summary>
/// Represents a named volume definition in a Docker Compose file.
/// </summary>
internal sealed class ComposeVolume
{
    /// <summary>
    /// Gets or sets the volume driver.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Resource.Driver)]
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets whether this is an external volume.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Resource.External)]
    public bool? External { get; set; }
}

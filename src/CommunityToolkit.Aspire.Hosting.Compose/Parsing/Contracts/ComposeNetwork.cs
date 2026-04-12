using YamlDotNet.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

/// <summary>
/// Represents a network definition in a Docker Compose file.
/// </summary>
internal sealed class ComposeNetwork
{
    /// <summary>
    /// Gets or sets the network driver.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Resource.Driver)]
    public string? Driver { get; set; }

    /// <summary>
    /// Gets or sets whether this is an external network.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Resource.External)]
    public bool? External { get; set; }
}

using YamlDotNet.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

/// <summary>
/// Represents a service defined in a Docker Compose file.
/// </summary>
internal sealed class ComposeService
{
    /// <summary>
    /// Gets or sets the container image.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Image)]
    public string? Image { get; set; }

    /// <summary>
    /// Gets or sets the container name override.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.ContainerName)]
    public string? ContainerName { get; set; }

    /// <summary>
    /// Gets or sets the hostname.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Hostname)]
    public string? Hostname { get; set; }

    /// <summary>
    /// Gets or sets the port mappings.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Ports)]
    public List<string>? Ports { get; set; }

    /// <summary>
    /// Gets or sets the environment variables. Supports both map and list syntax.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Environment)]
    public object? Environment { get; set; }

    /// <summary>
    /// Gets or sets the volume mounts.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.TopLevel.Volumes)]
    public List<string>? Volumes { get; set; }

    /// <summary>
    /// Gets or sets the service dependencies.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.DependsOn)]
    public object? DependsOn { get; set; }

    /// <summary>
    /// Gets or sets the command to run. Can be a string or list.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Command)]
    public object? Command { get; set; }

    /// <summary>
    /// Gets or sets the entrypoint. Can be a string or list.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Entrypoint)]
    public object? Entrypoint { get; set; }

    /// <summary>
    /// Gets or sets the healthcheck configuration.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Healthcheck)]
    public ComposeHealthcheck? Healthcheck { get; set; }

    /// <summary>
    /// Gets or sets the restart policy.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Restart)]
    public string? Restart { get; set; }

    /// <summary>
    /// Gets or sets the build configuration.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Service.Build)]
    public object? Build { get; set; }
}

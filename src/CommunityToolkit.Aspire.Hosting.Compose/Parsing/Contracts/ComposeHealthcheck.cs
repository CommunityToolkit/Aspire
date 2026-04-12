using YamlDotNet.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;

/// <summary>
/// Represents a healthcheck configuration in a Docker Compose service.
/// </summary>
internal sealed class ComposeHealthcheck
{
    /// <summary>
    /// Gets or sets the test command.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Health.Test)]
    public object? Test { get; set; }

    /// <summary>
    /// Gets or sets the interval between health checks.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Health.Interval)]
    public string? Interval { get; set; }

    /// <summary>
    /// Gets or sets the timeout for each check.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Health.Timeout)]
    public string? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the number of retries.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Health.Retries)]
    public int? Retries { get; set; }

    /// <summary>
    /// Gets or sets the start period.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Health.StartPeriod)]
    public string? StartPeriod { get; set; }

    /// <summary>
    /// Gets or sets whether the healthcheck is disabled.
    /// </summary>
    [YamlMember(Alias = ComposeConstants.Health.Disable)]
    public bool? Disable { get; set; }
}

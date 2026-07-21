using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Options for configuring the broker of a <see cref="RedPandaServerResource"/>.
/// </summary>
public class RedPandaServerOptions
{
    /// <summary>
    /// Gets or sets the number of logical CPU cores the broker is allowed to use
    /// (the Redpanda <c>--smp</c> flag). Defaults to <c>1</c>.
    /// </summary>
    public int CpuCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the amount of memory the broker is allowed to use
    /// (the Redpanda <c>--memory</c> flag), for example <c>"1G"</c>. Defaults to <c>"1G"</c>.
    /// </summary>
    public string Memory { get; set; } = "1G";
}

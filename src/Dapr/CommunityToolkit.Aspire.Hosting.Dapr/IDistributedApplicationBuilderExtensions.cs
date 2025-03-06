using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Aspire.Hosting;

public static partial class IDistributedApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Dapr support to Aspire, including the ability to add Dapr sidecar to application resource.
    /// </summary>
    /// <param name="builder">The distributed application builder instance.</param>
    /// <param name="configure">Callback to configure dapr options.</param>
    /// <returns>The distributed application builder instance.</returns>
    public static IDistributedApplicationBuilder AddDapr(this IDistributedApplicationBuilder builder, Action<DaprOptions>? configure = null)
    {
        return builder.AddDaprInternal<DefaultDaprPublishingHelper>(configure);
    }
}

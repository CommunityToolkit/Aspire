using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Flyway;

/// <summary>
/// Extension methods for configuring a <see cref="FlywayResource"/> builder.
/// </summary>
public static class FlywayResourceBuilderExtensions
{
    extension(IResourceBuilder<FlywayResource> builder)
    {
        /// <summary>
        /// Opts in to sending telemetry data to Redgate about Flyway usage.
        /// </summary>
        /// <returns>The updated <see cref="IResourceBuilder{T}"/>.</returns>
        public IResourceBuilder<FlywayResource> WithTelemetryOptIn()
        {
            builder.WithEnvironment("REDGATE_DISABLE_TELEMETRY", "false");
            return builder;
        }
    }
}

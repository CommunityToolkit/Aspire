using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

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
        [AspireExport("withTelemetryOptIn", Description = "Opts in to Redgate telemetry for a Flyway resource")]
        public IResourceBuilder<FlywayResource> WithTelemetryOptIn()
        {
            builder.WithEnvironment("REDGATE_DISABLE_TELEMETRY", "false");
            return builder;
        }
    }
}

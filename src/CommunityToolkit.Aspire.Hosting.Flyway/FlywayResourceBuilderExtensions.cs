using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001 // AspireExport is experimental
#pragma warning disable ASPIREEXPORT001 // AspireExport supports C# extension blocks even though the analyzer currently requires static methods.

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
        /// <ats-summary>Opts in to Redgate telemetry for a Flyway resource</ats-summary>
        [AspireExport]
        public IResourceBuilder<FlywayResource> WithTelemetryOptIn()
        {
            builder.WithEnvironment("REDGATE_DISABLE_TELEMETRY", "false");
            return builder;
        }
    }
}

#pragma warning restore ASPIREATS001
#pragma warning restore ASPIREEXPORT001

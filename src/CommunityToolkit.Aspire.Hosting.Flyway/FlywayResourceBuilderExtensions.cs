using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace CommunityToolkit.Aspire.Hosting.Flyway;

/// <summary>
/// Extension methods for configuring a <see cref="FlywayResource"/> builder.
/// </summary>
public static class FlywayResourceBuilderExtensions
{
    /// <summary>
    /// Opts in to sending telemetry data to Redgate about Flyway usage.
    /// </summary>
    /// <param name="builder">The Flyway resource builder.</param>
    /// <returns>The updated <see cref="IResourceBuilder{T}"/>.</returns>
    /// <ats-summary>Opts in to Redgate telemetry for a Flyway resource</ats-summary>
    [AspireExport]
    public static IResourceBuilder<FlywayResource> WithTelemetryOptIn(this IResourceBuilder<FlywayResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithEnvironment("REDGATE_DISABLE_TELEMETRY", "false");
        return builder;
    }
}

#pragma warning restore ASPIREATS001

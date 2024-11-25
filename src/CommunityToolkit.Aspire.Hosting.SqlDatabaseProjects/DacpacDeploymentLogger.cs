using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Provides source generated logging methods for the <see cref="DacpacDeployer"/> class.
/// </summary>
internal static partial class DacpacDeploymentLogger
{
    [LoggerMessage(0, LogLevel.Information, "{message}")]
    public static partial void LogDeploymentMessage(this ILogger logger, String message);
}
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Identifies the database target used by DACPAC pipeline operations.
/// </summary>
/// <param name="Target">The resource providing the target connection string.</param>
/// <param name="TargetDatabaseName">The optional database name override.</param>
internal sealed record DacpacTargetAnnotation(
    IResourceWithConnectionString Target,
    string? TargetDatabaseName) : IResourceAnnotation;

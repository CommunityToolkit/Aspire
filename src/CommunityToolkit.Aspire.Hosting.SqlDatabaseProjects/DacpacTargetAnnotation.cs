using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Identifies the database target used by DACPAC pipeline operations.
/// </summary>
/// <param name="Target">The resource providing the target connection string.</param>
/// <param name="TargetDatabaseName">The optional database name override.</param>
/// <param name="PipelineConnectionStringParameter">The optional parameter used instead of a run-mode local endpoint.</param>
internal sealed record DacpacTargetAnnotation(
    IResourceWithConnectionString Target,
    string? TargetDatabaseName,
    ParameterResource? PipelineConnectionStringParameter = null) : IResourceAnnotation;

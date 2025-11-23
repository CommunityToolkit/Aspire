using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Represents an annotation that identifies a target resource and optional database name for a DAC package deployment.
/// </summary>
/// <param name="target">The target resource to which the DAC package will be deployed. Must provide a valid connection string.</param>
/// <param name="targetDatabaseName">The name of the target database for deployment, or null to use the default database associated with the target
/// resource.</param>
internal class DacpackTargetAnnotation(IResourceWithConnectionString target, string? targetDatabaseName) : IResourceAnnotation
{
    /// <summary>
    /// The target resource the Dacpack will be deployed to
    /// </summary>
    public IResourceWithConnectionString Target => target;

    /// <summary>
    /// The name of the target database for the operation.
    /// </summary>
    public string? TargetDatabaseName => targetDatabaseName;
}
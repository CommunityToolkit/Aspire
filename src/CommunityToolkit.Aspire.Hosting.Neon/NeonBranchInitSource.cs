namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Specifies the source of initialization for a Neon branch.
/// </summary>
public enum NeonBranchInitSource
{
    /// <summary>
    /// Creates the branch with both schema and data from the parent (default Neon behavior).
    /// </summary>
    ParentData,

    /// <summary>
    /// Creates a new root branch containing only the schema from the parent.
    /// </summary>
    SchemaOnly
}

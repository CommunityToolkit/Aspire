namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Options for restoring (refreshing) a Neon branch from a source branch.
/// </summary>
public sealed class NeonBranchRestoreOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to restore the branch from its parent on each application run.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the source branch ID to restore from. If not specified, the branch is restored from its parent.
    /// </summary>
    public string? SourceBranchId { get; set; }

    /// <summary>
    /// Gets or sets a Log Sequence Number (LSN) on the source branch to restore from.
    /// </summary>
    public string? SourceLsn { get; set; }

    /// <summary>
    /// Gets or sets a point-in-time on the source branch to restore from.
    /// </summary>
    public DateTimeOffset? SourceTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the name to preserve the previous branch state under before restoring.
    /// </summary>
    /// <remarks>
    /// If the branch has children, this is required.
    /// </remarks>
    public string? PreserveUnderName { get; set; }
}

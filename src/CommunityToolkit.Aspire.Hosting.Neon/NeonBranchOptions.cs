namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Represents options for provisioning or connecting to a Neon branch.
/// </summary>
public sealed class NeonBranchOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to create a new ephemeral branch for each application run.
    /// </summary>
    public bool UseEphemeralBranch { get; set; }

    /// <summary>
    /// Gets or sets the prefix used when creating ephemeral branch names.
    /// </summary>
    public string EphemeralBranchPrefix { get; set; } = "aspire-";

    /// <summary>
    /// Gets or sets the Neon branch ID to connect to.
    /// </summary>
    public string? BranchId { get; set; }

    /// <summary>
    /// Gets or sets the Neon branch name to connect to or create.
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the branch should be protected from deletion.
    /// </summary>
    public bool? Protected { get; set; }

    /// <summary>
    /// Gets or sets the initialization source for the branch.
    /// </summary>
    /// <remarks>
    /// Valid values are <see cref="NeonBranchInitSource.ParentData"/> (default) which copies both
    /// schema and data from the parent, and <see cref="NeonBranchInitSource.SchemaOnly"/> which
    /// copies only the schema.
    /// </remarks>
    public NeonBranchInitSource? InitSource { get; set; }

    /// <summary>
    /// Gets or sets the expiration time for the branch.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create the branch if it does not exist.
    /// </summary>
    public bool CreateBranchIfMissing { get; set; }

    /// <summary>
    /// Gets or sets the parent branch ID to use when creating a new branch.
    /// </summary>
    public string? ParentBranchId { get; set; }

    /// <summary>
    /// Gets or sets the parent branch name to use when creating a new branch.
    /// </summary>
    public string? ParentBranchName { get; set; }

    /// <summary>
    /// Gets or sets a Log Sequence Number (LSN) on the parent branch to branch from.
    /// </summary>
    public string? ParentLsn { get; set; }

    /// <summary>
    /// Gets or sets a point-in-time on the parent branch to branch from.
    /// </summary>
    public DateTimeOffset? ParentTimestamp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the branch should be created in an archived state.
    /// </summary>
    public bool? Archived { get; set; }

    /// <summary>
    /// Gets or sets the compute endpoint ID to use for connections.
    /// </summary>
    public string? EndpointId { get; set; }

    /// <summary>
    /// Gets or sets the type of endpoint to create when one is missing.
    /// </summary>
    public NeonEndpointType EndpointType { get; set; } = NeonEndpointType.ReadWrite;

    /// <summary>
    /// Gets or sets a value indicating whether to create an endpoint if none is found.
    /// </summary>
    public bool CreateEndpointIfMissing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to set this branch as the project's default branch.
    /// </summary>
    public bool SetAsDefault { get; set; }

    /// <summary>
    /// Gets the restore options for refreshing the branch from a source branch.
    /// </summary>
    public NeonBranchRestoreOptions Restore { get; } = new();

    /// <summary>
    /// Gets the anonymization options for creating a branch with masked data.
    /// </summary>
    public NeonAnonymizationOptions Anonymization { get; } = new();
}
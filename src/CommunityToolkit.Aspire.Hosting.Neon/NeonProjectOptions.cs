namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Represents options for connecting to a Neon project and configuring the external provisioner.
/// </summary>
public sealed class NeonProjectOptions
{
    /// <summary>
    /// Gets or sets the Neon project ID to connect to.
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the Neon project name to connect to or create.
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to create the project if it does not exist.
    /// </summary>
    public bool CreateProjectIfMissing { get; set; }

    /// <summary>
    /// Gets or sets the Neon organization ID to create the project under.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the Neon organization name to resolve or create.
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>
    /// Gets or sets the Neon region ID to use when creating a project.
    /// </summary>
    public string? RegionId { get; set; }

    /// <summary>
    /// Gets or sets the PostgreSQL version to use when creating a project.
    /// </summary>
    public int? PostgresVersion { get; set; }

    /// <summary>
    /// Gets or sets the database name used when generating connection strings.
    /// </summary>
    public string? DatabaseName { get; set; } = "neondb";

    /// <summary>
    /// Gets or sets the role name used when generating connection strings.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to request a pooled connection string.
    /// </summary>
    public bool UseConnectionPooler { get; set; }

    /// <summary>
    /// Gets the branch configuration options.
    /// </summary>
    public NeonBranchOptions Branch { get; } = new();

    /// <summary>
    /// Gets the external provisioner execution options.
    /// </summary>
    public NeonProvisioningOptions Provisioning { get; } = new();
}
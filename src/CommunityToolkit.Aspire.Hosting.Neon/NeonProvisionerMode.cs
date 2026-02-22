namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Represents the execution mode for the external Neon provisioner.
/// </summary>
public enum NeonProvisionerMode
{
    /// <summary>
    /// Attach to existing Neon resources only. Missing resources cause a failure.
    /// </summary>
    Attach,

    /// <summary>
    /// Provision missing Neon resources as needed, then attach.
    /// </summary>
    Provision,
}
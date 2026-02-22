namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Represents external Neon provisioner execution options.
/// </summary>
public sealed class NeonProvisioningOptions
{
    /// <summary>
    /// Gets or sets the external provisioner mode.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="NeonProvisionerMode.Attach"/>, which validates and attaches to existing resources only.
    /// </remarks>
    public NeonProvisionerMode Mode { get; set; } = NeonProvisionerMode.Attach;
}
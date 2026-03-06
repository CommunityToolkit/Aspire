namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Options for creating an anonymized Neon branch with data masking.
/// </summary>
public sealed class NeonAnonymizationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether anonymization is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets the list of masking rules to apply to the anonymized branch.
    /// </summary>
    public List<NeonMaskingRule> MaskingRules { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to automatically start anonymization after the branch is created.
    /// </summary>
    public bool StartAnonymization { get; set; } = true;
}

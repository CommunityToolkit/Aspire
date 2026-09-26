namespace CommunityToolkit.Aspire.Hosting.k6;

/// <summary>
/// Define how detailed the end-of-test summary should be.
/// </summary>
public enum K6SummaryMode
{
    /// <summary>
    /// Default mode. Shows the most relevant test results in a concise format.
    /// </summary>
    Compact,
    /// <summary>
    /// Shows all available information.
    /// </summary>
    Full,
    /// <summary>
    /// Completely disables the summary generation.
    /// </summary>
    Disabled
}
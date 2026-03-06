namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Specifies the type of a Neon compute endpoint.
/// </summary>
public enum NeonEndpointType
{
    /// <summary>
    /// A read-write compute endpoint. Each branch supports one read-write endpoint.
    /// </summary>
    ReadWrite,

    /// <summary>
    /// A read-only compute endpoint. A branch can have multiple read-only endpoints.
    /// </summary>
    ReadOnly
}

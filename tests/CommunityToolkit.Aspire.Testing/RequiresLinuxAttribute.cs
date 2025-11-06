using Xunit.Sdk;

namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Marks a test or test class as requiring a Linux operating system.
/// Adds a trait so tests can be skipped or filtered when not running on Linux.
/// </summary>
[TraitDiscoverer("CommunityToolkit.Aspire.Testing.RequiresLinuxDiscoverer", "CommunityToolkit.Aspire.Testing")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiresLinuxAttribute(string? reason = null) : Attribute, ITraitAttribute
{
    /// <summary>Gets the optional reason why Linux is required.</summary>
    public string? Reason { get; init; } = reason;

    /// <summary>Gets a value indicating whether the current OS is Linux.</summary>
    public static bool IsSupported => OperatingSystem.IsLinux();
}

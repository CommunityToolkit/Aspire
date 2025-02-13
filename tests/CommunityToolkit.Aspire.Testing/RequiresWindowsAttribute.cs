using Xunit.Sdk;

namespace CommunityToolkit.Aspire.Testing;

[TraitDiscoverer("CommunityToolkit.Aspire.Testing.RequiresWindowsDiscoverer", "CommunityToolkit.Aspire.Testing")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiresWindowsAttribute(string? reason = null) : Attribute, ITraitAttribute
{
    public string? Reason { get; init; } = reason;

    public static bool IsSupported => OperatingSystem.IsWindows();
}

using Microsoft.DotNet.XUnitExtensions;
using Xunit.v3;

namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Marks a test or test class as requiring a Linux operating system.
/// Adds a trait so tests can be skipped or filtered when not running on Linux.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiresWindowsAttribute(string? reason = null) : Attribute, ITraitAttribute
{
    /// <summary>Gets the optional reason why Windows is required.</summary>
    public string? Reason { get; init; } = reason;

    /// <summary>Gets a value indicating whether the current OS is Windows.</summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
    {
        if (!IsSupported)
        {
            return [new KeyValuePair<string, string>(XunitConstants.Category, "failing")];
        }

        return [];
    }
}

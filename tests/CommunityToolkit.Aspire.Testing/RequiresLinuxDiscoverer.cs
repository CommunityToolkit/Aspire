using Xunit.Abstractions;
using Xunit.Sdk;

namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Discovers traits for <see cref="RequiresLinuxAttribute"/>.
/// Adds a category trait of <c>unsupported-platform</c> when not running on Linux.
/// </summary>
public class RequiresLinuxDiscoverer : ITraitDiscoverer
{
    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        if (!RequiresLinuxAttribute.IsSupported)
        {
            yield return new KeyValuePair<string, string>("category", "unsupported-platform");
        }
    }
}

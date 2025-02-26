using Xunit.Abstractions;
using Xunit.Sdk;

namespace CommunityToolkit.Aspire.Testing;

public class RequiresWindowsDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        if (!RequiresWindowsAttribute.IsSupported)
        {
            yield return new KeyValuePair<string, string>("category", "unsupported-platform");
        }
    }
}
using Xunit.Abstractions;
using Xunit.Sdk;

namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Discovers traits for <see cref="RequiresAuthenticatedToolAttribute"/>.
/// Adds a <c>RequiresTools</c> trait with the tool name and
/// categorizes tests as <c>unsupported-platform</c> when authenticated tools are not supported.
/// </summary>
public sealed class RequiresAuthenticatedToolDiscoverer : ITraitDiscoverer
{
    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        string? toolName = null;
        foreach (object? argument in traitAttribute.GetConstructorArguments())
        {
            if (argument is string value)
            {
                toolName = value;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(toolName))
        {
            yield return new KeyValuePair<string, string>("RequiresTools", toolName);
        }

        if (!RequiresAuthenticatedToolAttribute.IsSupported)
        {
            yield return new KeyValuePair<string, string>("category", "unsupported-platform");
        }
    }
}

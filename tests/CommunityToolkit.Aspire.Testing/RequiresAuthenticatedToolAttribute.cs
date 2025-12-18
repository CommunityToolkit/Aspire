using Aspire.Components.Common.Tests;
using Microsoft.DotNet.XUnitExtensions;
using Xunit.v3;

namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Marks a test or test class as requiring an authenticated external tool.
/// Adds a trait to propagate the required tool name to the xUnit pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresAuthenticatedToolAttribute : Attribute, ITraitAttribute
{
    /// <summary>Initializes a new instance of the <see cref="RequiresAuthenticatedToolAttribute"/> class.</summary>
    /// <param name="toolName">The name of the external tool required for the test.</param>
    /// <param name="reason">An optional reason describing why the tool is required.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="toolName"/> is null or whitespace.</exception>
    public RequiresAuthenticatedToolAttribute(string toolName, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("Tool name cannot be null or whitespace.", nameof(toolName));
        }

        ToolName = toolName;
        Reason = reason;
    }

    /// <summary>Gets the name of the external tool required for the test.</summary>
    public string ToolName { get; }

    /// <summary>Gets the optional reason describing why the tool is required.</summary>
    public string? Reason { get; }

    /// <summary>Gets a value indicating whether authenticated tools are supported in the current environment.</summary>
    public static bool IsSupported => !PlatformDetection.IsRunningOnCI;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
    {
        IReadOnlyCollection<KeyValuePair<string, string>> traits = [new("RequiresTools", ToolName)];

        if (!IsSupported)
        {
            traits = [.. traits, new KeyValuePair<string, string>(XunitConstants.Category, "unsupported-platform")];
        }

        return traits;
    }
}

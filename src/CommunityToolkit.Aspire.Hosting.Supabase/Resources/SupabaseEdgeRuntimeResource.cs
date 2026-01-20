using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Resources;

/// <summary>
/// Represents a Supabase Edge Runtime container resource for Edge Functions.
/// </summary>
public sealed class SupabaseEdgeRuntimeResource : ContainerResource
{
    /// <summary>
    /// Creates a new instance of the SupabaseEdgeRuntimeResource.
    /// </summary>
    /// <param name="name">The name of the edge runtime container.</param>
    public SupabaseEdgeRuntimeResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the internal port for the edge runtime.
    /// </summary>
    public int Port { get; internal set; } = 9000;

    /// <summary>
    /// Gets the list of function names available in this runtime.
    /// </summary>
    public List<string> FunctionNames { get; } = [];

    /// <summary>
    /// Gets or sets the path to the edge functions directory.
    /// </summary>
    public string? FunctionsPath { get; internal set; }

    /// <summary>
    /// Gets or sets the reference to the parent stack.
    /// </summary>
    internal SupabaseStackResource? Stack { get; set; }
}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase Edge Runtime.
/// </summary>
public static class EdgeRuntimeBuilderExtensions
{
    /// <summary>
    /// Configures the Edge Runtime settings.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the Edge Runtime resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureEdgeRuntime(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseEdgeRuntimeResource>> configure)
    {
        var stack = builder.Resource;
        if (stack.EdgeRuntime == null)
            throw new InvalidOperationException("EdgeRuntime not configured. Ensure WithEdgeFunctions() has been called.");

        configure(stack.EdgeRuntime);
        return builder;
    }

    /// <summary>
    /// Sets the internal Edge Runtime port.
    /// </summary>
    public static IResourceBuilder<SupabaseEdgeRuntimeResource> WithPort(
        this IResourceBuilder<SupabaseEdgeRuntimeResource> builder,
        int port)
    {
        builder.Resource.Port = port;
        builder.WithEnvironment("EDGE_RUNTIME_PORT", port.ToString());
        return builder;
    }

    /// <summary>
    /// Sets a custom environment variable for the Edge Runtime.
    /// </summary>
    public static IResourceBuilder<SupabaseEdgeRuntimeResource> WithCustomEnvironment(
        this IResourceBuilder<SupabaseEdgeRuntimeResource> builder,
        string name,
        string value)
    {
        builder.WithEnvironment(name, value);
        return builder;
    }
}

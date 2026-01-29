using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase Postgres-Meta service.
/// </summary>
public static class MetaBuilderExtensions
{
    /// <summary>
    /// Configures the Postgres-Meta settings.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the Meta resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureMeta(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseMetaResource>> configure)
    {
        var stack = builder.Resource;
        if (stack.Meta == null)
            throw new InvalidOperationException("Meta not configured. Ensure AddSupabase() has been called.");

        configure(stack.Meta);
        return builder;
    }

    /// <summary>
    /// Sets the internal Postgres-Meta port.
    /// </summary>
    public static IResourceBuilder<SupabaseMetaResource> WithPort(
        this IResourceBuilder<SupabaseMetaResource> builder,
        int port)
    {
        builder.Resource.Port = port;
        builder.WithEnvironment("PG_META_PORT", port.ToString());
        return builder;
    }
}

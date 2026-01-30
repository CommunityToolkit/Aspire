using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase Kong API Gateway.
/// </summary>
public static class KongBuilderExtensions
{
    /// <summary>
    /// Configures the Kong API Gateway settings.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the Kong resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureKong(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseKongResource>> configure)
    {
        var stack = builder.Resource;
        if (stack.Kong == null)
            throw new InvalidOperationException("Kong not configured. Ensure AddSupabase() has been called.");

        configure(stack.Kong);
        return builder;
    }

    /// <summary>
    /// Sets the external Kong port.
    /// </summary>
    public static IResourceBuilder<SupabaseKongResource> WithPort(
        this IResourceBuilder<SupabaseKongResource> builder,
        int port)
    {
        builder.Resource.ExternalPort = port;
        return builder;
    }

    /// <summary>
    /// Sets the Kong plugins to enable.
    /// </summary>
    public static IResourceBuilder<SupabaseKongResource> WithPlugins(
        this IResourceBuilder<SupabaseKongResource> builder,
        params string[] plugins)
    {
        builder.Resource.Plugins = plugins;
        builder.WithEnvironment("KONG_PLUGINS", string.Join(",", plugins));
        return builder;
    }
}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase REST API (PostgREST).
/// </summary>
public static class RestBuilderExtensions
{
    /// <summary>
    /// Configures the PostgREST settings.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the REST resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureRest(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseRestResource>> configure)
    {
        var stack = builder.Resource;
        if (stack.Rest == null)
            throw new InvalidOperationException("Rest not configured. Ensure AddSupabase() has been called.");

        configure(stack.Rest);
        return builder;
    }

    /// <summary>
    /// Sets the database schemas exposed by PostgREST.
    /// </summary>
    public static IResourceBuilder<SupabaseRestResource> WithSchemas(
        this IResourceBuilder<SupabaseRestResource> builder,
        params string[] schemas)
    {
        builder.Resource.Schemas = schemas;
        builder.WithEnvironment("PGRST_DB_SCHEMAS", string.Join(",", schemas));
        return builder;
    }

    /// <summary>
    /// Sets the anonymous role name for unauthenticated requests.
    /// </summary>
    public static IResourceBuilder<SupabaseRestResource> WithAnonRole(
        this IResourceBuilder<SupabaseRestResource> builder,
        string role)
    {
        builder.Resource.AnonRole = role;
        builder.WithEnvironment("PGRST_DB_ANON_ROLE", role);
        return builder;
    }
}

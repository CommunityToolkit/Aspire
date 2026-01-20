using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase Auth (GoTrue).
/// </summary>
public static class AuthBuilderExtensions
{
    /// <summary>
    /// Configures the GoTrue authentication settings.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the auth resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureAuth(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseAuthResource>> configure)
    {
        var stack = builder.Resource;
        if (stack.Auth == null)
            throw new InvalidOperationException("Auth not configured. Ensure AddSupabase() has been called.");

        configure(stack.Auth);
        return builder;
    }

    /// <summary>
    /// Sets the site URL for authentication redirects.
    /// </summary>
    public static IResourceBuilder<SupabaseAuthResource> WithSiteUrl(
        this IResourceBuilder<SupabaseAuthResource> builder,
        string url)
    {
        builder.Resource.SiteUrl = url;
        builder.WithEnvironment("GOTRUE_SITE_URL", url);
        return builder;
    }

    /// <summary>
    /// Enables or disables auto-confirmation of email addresses.
    /// </summary>
    public static IResourceBuilder<SupabaseAuthResource> WithAutoConfirm(
        this IResourceBuilder<SupabaseAuthResource> builder,
        bool enabled = true)
    {
        builder.Resource.AutoConfirm = enabled;
        builder.WithEnvironment("GOTRUE_MAILER_AUTOCONFIRM", enabled ? "true" : "false");
        return builder;
    }

    /// <summary>
    /// Enables or disables user signup.
    /// </summary>
    public static IResourceBuilder<SupabaseAuthResource> WithDisableSignup(
        this IResourceBuilder<SupabaseAuthResource> builder,
        bool disabled = true)
    {
        builder.Resource.DisableSignup = disabled;
        builder.WithEnvironment("GOTRUE_DISABLE_SIGNUP", disabled ? "true" : "false");
        return builder;
    }

    /// <summary>
    /// Enables or disables anonymous users.
    /// </summary>
    public static IResourceBuilder<SupabaseAuthResource> WithAnonymousUsers(
        this IResourceBuilder<SupabaseAuthResource> builder,
        bool enabled = true)
    {
        builder.Resource.AnonymousUsersEnabled = enabled;
        builder.WithEnvironment("GOTRUE_ANONYMOUS_USERS_ENABLED", enabled ? "true" : "false");
        return builder;
    }

    /// <summary>
    /// Sets the JWT expiration time in seconds.
    /// </summary>
    public static IResourceBuilder<SupabaseAuthResource> WithJwtExpiration(
        this IResourceBuilder<SupabaseAuthResource> builder,
        int seconds)
    {
        builder.Resource.JwtExpiration = seconds;
        builder.WithEnvironment("GOTRUE_JWT_EXP", seconds.ToString());
        return builder;
    }
}

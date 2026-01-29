using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase Studio Dashboard.
/// Note: The SupabaseStackResource IS the Studio container.
/// </summary>
public static class StudioBuilderExtensions
{
    /// <summary>
    /// Configures the Studio Dashboard settings.
    /// Since the SupabaseStackResource IS the Studio container, this configures the stack itself.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the Studio (stack) resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureStudio(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseStackResource>> configure)
    {
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Sets the external Studio port.
    /// </summary>
    public static IResourceBuilder<SupabaseStackResource> WithStudioPort(
        this IResourceBuilder<SupabaseStackResource> builder,
        int port)
    {
        builder.Resource.StudioPort = port;
        return builder;
    }

    /// <summary>
    /// Sets the organization name displayed in Studio.
    /// </summary>
    public static IResourceBuilder<SupabaseStackResource> WithOrganizationName(
        this IResourceBuilder<SupabaseStackResource> builder,
        string name)
    {
        builder.WithEnvironment("DEFAULT_ORGANIZATION_NAME", name);
        return builder;
    }

    /// <summary>
    /// Sets the project name displayed in Studio.
    /// </summary>
    public static IResourceBuilder<SupabaseStackResource> WithProjectName(
        this IResourceBuilder<SupabaseStackResource> builder,
        string name)
    {
        builder.WithEnvironment("DEFAULT_PROJECT_NAME", name);
        return builder;
    }
}

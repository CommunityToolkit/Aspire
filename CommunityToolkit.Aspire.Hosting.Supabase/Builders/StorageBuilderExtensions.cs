using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase Storage API.
/// </summary>
public static class StorageBuilderExtensions
{
    /// <summary>
    /// Configures the Storage API settings.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the Storage resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureStorage(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseStorageResource>> configure)
    {
        var stack = builder.Resource;
        if (stack.Storage == null)
            throw new InvalidOperationException("Storage not configured. Ensure AddSupabase() has been called.");

        configure(stack.Storage);
        return builder;
    }

    /// <summary>
    /// Sets the maximum file size limit in bytes.
    /// </summary>
    public static IResourceBuilder<SupabaseStorageResource> WithFileSizeLimit(
        this IResourceBuilder<SupabaseStorageResource> builder,
        long bytes)
    {
        builder.Resource.FileSizeLimit = bytes;
        builder.WithEnvironment("FILE_SIZE_LIMIT", bytes.ToString());
        return builder;
    }

    /// <summary>
    /// Sets the storage backend type.
    /// </summary>
    public static IResourceBuilder<SupabaseStorageResource> WithBackend(
        this IResourceBuilder<SupabaseStorageResource> builder,
        string backend)
    {
        builder.Resource.Backend = backend;
        builder.WithEnvironment("STORAGE_BACKEND", backend);
        return builder;
    }

    /// <summary>
    /// Enables or disables image transformation.
    /// </summary>
    public static IResourceBuilder<SupabaseStorageResource> WithImageTransformation(
        this IResourceBuilder<SupabaseStorageResource> builder,
        bool enabled = true)
    {
        builder.Resource.EnableImageTransformation = enabled;
        builder.WithEnvironment("ENABLE_IMAGE_TRANSFORMATION", enabled ? "true" : "false");
        return builder;
    }
}

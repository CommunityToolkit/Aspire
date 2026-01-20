using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Builders;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Sync;

/// <summary>
/// Provides extension methods for project synchronization.
/// </summary>
public static class ProjectSyncExtensions
{
    /// <summary>
    /// Enables synchronization from an online Supabase project.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="projectRef">The Supabase project reference ID.</param>
    /// <param name="serviceKey">The service role key for API access.</param>
    /// <param name="options">What to synchronize (default: All).</param>
    /// <param name="dbPassword">Database password for full pg_dump sync. Required for AllSchema options.</param>
    /// <param name="managementApiToken">Supabase Management API token for Edge Functions sync.</param>
    public static IResourceBuilder<SupabaseStackResource> WithProjectSync(
        this IResourceBuilder<SupabaseStackResource> builder,
        string? projectRef,
        string? serviceKey,
        SyncOptions options = SyncOptions.All,
        string? dbPassword = null,
        string? managementApiToken = null)
    {
        // Validate parameters
        if (string.IsNullOrWhiteSpace(projectRef))
        {
            Console.WriteLine("[Supabase Sync] SKIPPED: projectRef is empty or null.");
            return builder;
        }

        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            Console.WriteLine("[Supabase Sync] SKIPPED: serviceKey is empty or null.");
            return builder;
        }

        // Check if key is in JWT format (not CLI format)
        if (!serviceKey.StartsWith("eyJ"))
        {
            Console.WriteLine("[Supabase Sync] ERROR: serviceKey has wrong format!");
            Console.WriteLine("                Expected: JWT format (starts with 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...')");
            Console.WriteLine("                Found: '" + serviceKey.Substring(0, Math.Min(20, serviceKey.Length)) + "...'");
            Console.WriteLine("                Note: Use the 'service_role' key from Dashboard → Project Settings → API");
            Console.WriteLine("                      NOT the 'sb_secret_...' key - that's only for the Supabase CLI!");
            return builder;
        }

        // Warn if schema options require dbPassword but none provided
        bool needsDbPassword = options.HasFlag(SyncOptions.Policies) ||
                               options.HasFlag(SyncOptions.Functions) ||
                               options.HasFlag(SyncOptions.Triggers) ||
                               options.HasFlag(SyncOptions.Types) ||
                               options.HasFlag(SyncOptions.Views) ||
                               options.HasFlag(SyncOptions.Indexes);

        if (needsDbPassword && string.IsNullOrWhiteSpace(dbPassword))
        {
            Console.WriteLine("[Supabase Sync] WARNING: For complete schema sync (Policies, Functions, Triggers, Views) the DB password is required!");
            Console.WriteLine("                Note: Dashboard → Project Settings → Database → Database password");
            Console.WriteLine("                The sync options Policies/Functions/Triggers/Types/Views/Indexes will be skipped.");
        }

        // Warn if Edge Functions sync requires management API token
        if (options.HasFlag(SyncOptions.EdgeFunctions) && string.IsNullOrWhiteSpace(managementApiToken))
        {
            Console.WriteLine("[Supabase Sync] WARNING: For Edge Functions sync a Management API token is required!");
            Console.WriteLine("                Note: Dashboard → Account → Access Tokens → Generate new token");
            Console.WriteLine("                The sync option EdgeFunctions will be skipped.");
        }

        // Store configuration
        builder.Resource.SyncFromProjectRef = projectRef;
        builder.Resource.SyncServiceKey = serviceKey;
        builder.Resource.SyncSchema = options.HasFlag(SyncOptions.Schema);
        builder.Resource.SyncData = options.HasFlag(SyncOptions.Data);

        // Perform sync now - the init directory should exist from AddSupabase
        if (string.IsNullOrEmpty(builder.Resource.InitSqlPath))
        {
            Console.WriteLine("[Supabase Sync] ERROR: InitSqlPath not set. Was AddSupabase() called?");
            return builder;
        }

        // Determine storage path for file downloads
        var infraDir = Path.GetDirectoryName(builder.Resource.InitSqlPath)!;
        var storagePath = Path.Combine(infraDir, "storage");

        // Determine edge functions path for Edge Functions sync
        var edgeFunctionsPath = Path.Combine(infraDir, "edge-functions");

        try
        {
            SyncService.SyncFromOnlineProject(
                builder.Resource.InitSqlPath!,
                projectRef,
                serviceKey,
                options,
                dbPassword,
                storagePath,
                managementApiToken,
                edgeFunctionsPath).GetAwaiter().GetResult();

            // If Edge Functions were synced, enable them
            if (options.HasFlag(SyncOptions.EdgeFunctions) &&
                !string.IsNullOrWhiteSpace(managementApiToken) &&
                Directory.Exists(edgeFunctionsPath) &&
                Directory.GetDirectories(edgeFunctionsPath).Length > 0)
            {
                Console.WriteLine($"[Supabase Sync] Enabling synchronized Edge Functions from: {edgeFunctionsPath}");
                builder.WithEdgeFunctions(edgeFunctionsPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Sync] ERROR: {ex.Message}");
        }

        return builder;
    }

}

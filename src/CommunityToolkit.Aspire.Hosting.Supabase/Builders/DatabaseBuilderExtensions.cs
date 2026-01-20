using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Helpers;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for configuring the Supabase Database (PostgreSQL).
/// </summary>
public static class DatabaseBuilderExtensions
{
    private const int PostgresPort = 5432;

    /// <summary>
    /// Configures the PostgreSQL database settings.
    /// </summary>
    /// <param name="builder">The Supabase stack resource builder.</param>
    /// <param name="configure">Configuration action for the database resource builder.</param>
    /// <returns>The Supabase stack resource builder for chaining.</returns>
    public static IResourceBuilder<SupabaseStackResource> ConfigureDatabase(
        this IResourceBuilder<SupabaseStackResource> builder,
        Action<IResourceBuilder<SupabaseDatabaseResource>> configure)
    {
        var stack = builder.Resource;
        if (stack.Database == null)
            throw new InvalidOperationException("Database not configured. Ensure AddSupabase() has been called.");

        configure(stack.Database);
        return builder;
    }

    /// <summary>
    /// Sets the PostgreSQL password and updates all dependent containers.
    /// </summary>
    public static IResourceBuilder<SupabaseDatabaseResource> WithPassword(
        this IResourceBuilder<SupabaseDatabaseResource> builder,
        string password)
    {
        var resource = builder.Resource;
        resource.Password = password;

        var stack = resource.Stack;
        if (stack == null)
            throw new InvalidOperationException("Stack not configured on database resource.");

        var containerPrefix = stack.Name;

        // Update environment variables on all containers that use the password
        builder.WithEnvironment("POSTGRES_PASSWORD", password);

        // Auth container - update DB URL
        var authDbUrl = $"postgres://supabase_auth_admin:{password}@{containerPrefix}-db:{PostgresPort}/postgres?search_path=auth";
        stack.Auth?.WithEnvironment("GOTRUE_DB_DATABASE_URL", authDbUrl);

        // Rest container - update DB URI
        var restDbUri = $"postgres://authenticator:{password}@{containerPrefix}-db:{PostgresPort}/postgres";
        stack.Rest?.WithEnvironment("PGRST_DB_URI", restDbUri);

        // Storage container - update DB URL
        var storageDatabaseUrl = $"postgres://supabase_storage_admin:{password}@{containerPrefix}-db:{PostgresPort}/postgres";
        stack.Storage?.WithEnvironment("DATABASE_URL", storageDatabaseUrl);

        // Meta container - update password
        stack.Meta?.WithEnvironment("PG_META_DB_PASSWORD", password);

        // Studio container (which is the stack itself) - update password
        stack.StackBuilder?.WithEnvironment("POSTGRES_PASSWORD", password);

        // Re-generate SQL files with the new password
        if (!string.IsNullOrEmpty(stack.InitSqlPath))
        {
            // Update 00_init.sql with new password
            SupabaseSqlGenerator.WriteInitSql(stack.InitSqlPath, password);

            // Update post_init.sh with new password
            var scriptsDir = Path.Combine(Path.GetDirectoryName(stack.InitSqlPath)!, "scripts");
            var postInitShPath = Path.Combine(scriptsDir, "post_init.sh");
            if (Directory.Exists(scriptsDir))
            {
                SupabaseSqlGenerator.WritePostInitScript(postInitShPath, $"{containerPrefix}-db", password);
            }

            Console.WriteLine($"[Supabase] Database password updated in all containers and SQL files");
        }

        return builder;
    }

    /// <summary>
    /// Sets the external PostgreSQL port.
    /// </summary>
    public static IResourceBuilder<SupabaseDatabaseResource> WithPort(
        this IResourceBuilder<SupabaseDatabaseResource> builder,
        int port)
    {
        builder.Resource.ExternalPort = port;
        return builder;
    }
}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Helpers;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides the main extension method for adding Supabase to an Aspire application.
/// </summary>
public static class SupabaseBuilderExtensions
{
    #region Constants

    internal static class Images
    {
        public const string Postgres = "supabase/postgres";
        public const string PostgresTag = "15.1.1.78";
        public const string GoTrue = "supabase/gotrue";
        public const string GoTrueTag = "v2.185.0";
        public const string PostgREST = "postgrest/postgrest";
        public const string PostgRESTTag = "v12.2.0";
        public const string StorageApi = "supabase/storage-api";
        public const string StorageApiTag = "v1.11.13";
        public const string Kong = "kong";
        public const string KongTag = "2.8.1";
        public const string PostgresMeta = "supabase/postgres-meta";
        public const string PostgresMetaTag = "v0.84.2";
        public const string Studio = "supabase/studio";
        public const string StudioTag = "latest";
        public const string EdgeRuntime = "supabase/edge-runtime";
        public const string EdgeRuntimeTag = "v1.67.4";
    }

    internal static class Ports
    {
        public const int Postgres = 5432;
        public const int GoTrue = 9999;
        public const int PostgREST = 3000;
        public const int StorageApi = 5000;
        public const int Kong = 8000;
        public const int PostgresMeta = 8080;
        public const int Studio = 3000;
        public const int EdgeRuntime = 9000;
    }

    internal static class Defaults
    {
        public const string JwtSecret = "super-secret-jwt-token-with-at-least-32-characters-long";
        public const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6ImFub24iLCJleHAiOjE5ODM4MTI5OTZ9.CRXP1A7WOeoJeXxjNni43kdQwgnWNReilDMblYTn_I0";
        public const string ServiceRoleKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZS1kZW1vIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImV4cCI6MTk4MzgxMjk5Nn0.EGIM96RAZx35lJzdJsyH-qQwv8Hdp7fsn3W0YpN81IU";
        public const string Password = "postgres-insecure-dev-password";
        public const int ExternalPostgresPort = 54322;
        public const int ExternalKongPort = 8000;
        public const int ExternalStudioPort = 54323;
    }

    #endregion

    #region Clear Infrastructure

    /// <summary>
    /// Clears all Supabase infrastructure (Docker containers, volumes, and data files).
    /// Call this before AddSupabase() for a clean start.
    /// </summary>
    public static IDistributedApplicationBuilder ClearSupabase(
        this IDistributedApplicationBuilder builder,
        string containerPrefix = "supabase")
    {
        Console.WriteLine("[Supabase Clear] Clearing Supabase infrastructure...");

        var containerNames = new[]
        {
            containerPrefix,
            $"{containerPrefix}-db",
            $"{containerPrefix}-auth",
            $"{containerPrefix}-rest",
            $"{containerPrefix}-storage",
            $"{containerPrefix}-kong",
            $"{containerPrefix}-meta",
            $"{containerPrefix}-edge",
            $"{containerPrefix}-init"
        };

        foreach (var container in containerNames)
        {
            try
            {
                var stopProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"rm -f {container}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                stopProcess?.WaitForExit(5000);
            }
            catch { /* Ignore errors if container doesn't exist */ }
        }

        var infraDir = Path.Combine(builder.AppHostDirectory, "..", "infra", "supabase");
        if (Directory.Exists(infraDir))
        {
            try
            {
                Directory.Delete(infraDir, recursive: true);
                Console.WriteLine($"[Supabase Clear] Directory deleted: {infraDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Clear] WARNING: Could not delete directory: {ex.Message}");
            }
        }

        Console.WriteLine("[Supabase Clear] Cleanup completed.");
        return builder;
    }

    #endregion

    #region Main Entry Point

    /// <summary>
    /// Adds a complete Supabase stack to the application.
    /// The returned resource IS the Studio Dashboard container and serves as the visual parent
    /// for all other Supabase containers in the Aspire dashboard.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Supabase resource (will appear as "supabase" in dashboard).</param>
    /// <returns>A resource builder for further configuration.</returns>
    public static IResourceBuilder<SupabaseStackResource> AddSupabase(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        // Create the main stack resource (which IS the Studio container)
        var stack = new SupabaseStackResource(name)
        {
            JwtSecret = Defaults.JwtSecret,
            AnonKey = Defaults.AnonKey,
            ServiceRoleKey = Defaults.ServiceRoleKey,
            AppBuilder = builder
        };

        // Ensure directories exist
        var rootDir = Path.Combine(builder.AppHostDirectory, "..", "infra", "supabase");
        var dirs = EnsureDirectories(rootDir);
        stack.InfraRootDir = rootDir;
        stack.InitSqlPath = dirs.Init;

        var containerPrefix = name;

        // --- Create typed container resources ---

        // DATABASE
        var dbResource = new SupabaseDatabaseResource($"{containerPrefix}-db")
        {
            Password = Defaults.Password,
            ExternalPort = Defaults.ExternalPostgresPort,
            Stack = stack
        };

        // Create initial configuration files with default password
        SupabaseSqlGenerator.WriteInitSql(dirs.Init, dbResource.Password);

        stack.Database = builder.AddResource(dbResource)
            .WithImage(Images.Postgres, Images.PostgresTag)
            .WithContainerName($"{containerPrefix}-db")
            .WithEnvironment("POSTGRES_PASSWORD", dbResource.Password)
            .WithEnvironment("POSTGRES_DB", "postgres")
            .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
            .WithBindMount(dirs.Data, "/var/lib/postgresql/data")
            .WithBindMount(dirs.Init, "/docker-entrypoint-initdb.d", isReadOnly: true)
            .WithEndpoint(port: dbResource.ExternalPort, targetPort: Ports.Postgres, name: "tcp", scheme: "tcp");

        // AUTH (GoTrue)
        var authResource = new SupabaseAuthResource($"{containerPrefix}-auth") { Stack = stack };
        var authDbUrl = $"postgres://supabase_auth_admin:{dbResource.Password}@{containerPrefix}-db:{Ports.Postgres}/postgres?search_path=auth";

        stack.Auth = builder.AddResource(authResource)
            .WithImage(Images.GoTrue, Images.GoTrueTag)
            .WithContainerName($"{containerPrefix}-auth")
            .WithEnvironment("GOTRUE_API_HOST", "0.0.0.0")
            .WithEnvironment("GOTRUE_API_PORT", Ports.GoTrue.ToString())
            .WithEnvironment("GOTRUE_DB_DRIVER", "postgres")
            .WithEnvironment("GOTRUE_DB_DATABASE_URL", authDbUrl)
            .WithEnvironment("GOTRUE_DB_NAMESPACE", "auth")
            .WithEnvironment("GOTRUE_SITE_URL", authResource.SiteUrl)
            .WithEnvironment("API_EXTERNAL_URL", $"http://localhost:{Defaults.ExternalKongPort.ToString()}")
            .WithEnvironment("GOTRUE_URI_ALLOW_LIST", "*")
            .WithEnvironment("GOTRUE_JWT_SECRET", stack.JwtSecret)
            .WithEnvironment("GOTRUE_JWT_EXP", authResource.JwtExpiration.ToString())
            .WithEnvironment("GOTRUE_JWT_DEFAULT_GROUP_NAME", "authenticated")
            .WithEnvironment("GOTRUE_JWT_ADMIN_ROLES", "service_role")
            .WithEnvironment("GOTRUE_JWT_AUD", "authenticated")
            .WithEnvironment("GOTRUE_EXTERNAL_EMAIL_ENABLED", "true")
            .WithEnvironment("GOTRUE_MAILER_AUTOCONFIRM", authResource.AutoConfirm ? "true" : "false")
            .WithEnvironment("GOTRUE_MAILER_SECURE_EMAIL_CHANGE_ENABLED", "false")
            .WithEnvironment("GOTRUE_DISABLE_SIGNUP", authResource.DisableSignup ? "true" : "false")
            .WithEnvironment("GOTRUE_ANONYMOUS_USERS_ENABLED", authResource.AnonymousUsersEnabled ? "true" : "false")
            .WithEnvironment("GOTRUE_RATE_LIMIT_HEADER", "X-Forwarded-For")
            .WithEnvironment("GOTRUE_RATE_LIMIT_EMAIL_SENT", "100")
            .WithHttpEndpoint(targetPort: Ports.GoTrue, name: "http")
            .WithContainerRuntimeArgs("--restart=on-failure:10")
            .WaitFor(stack.Database);

        // REST (PostgREST)
        var restResource = new SupabaseRestResource($"{containerPrefix}-rest") { Stack = stack };
        var restDbUri = $"postgres://authenticator:{dbResource.Password}@{containerPrefix}-db:{Ports.Postgres}/postgres";

        stack.Rest = builder.AddResource(restResource)
            .WithImage(Images.PostgREST, Images.PostgRESTTag)
            .WithContainerName($"{containerPrefix}-rest")
            .WithEnvironment("PGRST_DB_URI", restDbUri)
            .WithEnvironment("PGRST_DB_SCHEMAS", string.Join(",", restResource.Schemas))
            .WithEnvironment("PGRST_DB_ANON_ROLE", restResource.AnonRole)
            .WithEnvironment("PGRST_JWT_SECRET", stack.JwtSecret)
            .WithEnvironment("PGRST_DB_USE_LEGACY_GUCS", "false")
            .WithHttpEndpoint(targetPort: Ports.PostgREST, name: "http")
            .WithContainerRuntimeArgs("--restart=on-failure:10")
            .WaitFor(stack.Database);

        // STORAGE
        var storageResource = new SupabaseStorageResource($"{containerPrefix}-storage") { Stack = stack };
        var storageDatabaseUrl = $"postgres://supabase_storage_admin:{dbResource.Password}@{containerPrefix}-db:{Ports.Postgres}/postgres";

        stack.Storage = builder.AddResource(storageResource)
            .WithImage(Images.StorageApi, Images.StorageApiTag)
            .WithContainerName($"{containerPrefix}-storage")
            .WithEnvironment("ANON_KEY", stack.AnonKey)
            .WithEnvironment("SERVICE_KEY", stack.ServiceRoleKey)
            .WithEnvironment("POSTGREST_URL", $"http://{containerPrefix}-rest:{Ports.PostgREST.ToString()}")
            .WithEnvironment("PGRST_JWT_SECRET", stack.JwtSecret)
            .WithEnvironment("DATABASE_URL", storageDatabaseUrl)
            .WithEnvironment("FILE_STORAGE_BACKEND_PATH", "/var/lib/storage")
            .WithEnvironment("STORAGE_BACKEND", storageResource.Backend)
            .WithEnvironment("FILE_SIZE_LIMIT", storageResource.FileSizeLimit.ToString())
            .WithEnvironment("TENANT_ID", "stub")
            .WithEnvironment("REGION", "local")
            .WithEnvironment("GLOBAL_S3_BUCKET", "stub")
            .WithEnvironment("IS_MULTITENANT", "false")
            .WithEnvironment("ENABLE_IMAGE_TRANSFORMATION", storageResource.EnableImageTransformation ? "true" : "false")
            .WithBindMount(dirs.Storage, "/var/lib/storage")
            .WithHttpEndpoint(targetPort: Ports.StorageApi, name: "http")
            .WithContainerRuntimeArgs("--restart=on-failure:10")
            .WaitFor(stack.Database)
            .WaitFor(stack.Rest);

        // KONG (API Gateway)
        var kongResource = new SupabaseKongResource($"{containerPrefix}-kong")
        {
            ExternalPort = Defaults.ExternalKongPort,
            Stack = stack
        };

        // Generate Kong config
        SupabaseSqlGenerator.WriteKongConfig(
            Path.Combine(dirs.Config, "kong.yml"),
            stack.AnonKey,
            stack.ServiceRoleKey,
            containerPrefix,
            Ports.GoTrue,
            Ports.PostgREST,
            Ports.StorageApi,
            Ports.PostgresMeta,
            Ports.EdgeRuntime);

        stack.Kong = builder.AddResource(kongResource)
            .WithImage(Images.Kong, Images.KongTag)
            .WithContainerName($"{containerPrefix}-kong")
            .WithEnvironment("KONG_DATABASE", "off")
            .WithEnvironment("KONG_DECLARATIVE_CONFIG", "/home/kong/kong.yml")
            .WithEnvironment("KONG_DNS_ORDER", "LAST,A,CNAME")
            .WithEnvironment("KONG_PLUGINS", string.Join(",", kongResource.Plugins))
            .WithEnvironment("KONG_NGINX_PROXY_PROXY_BUFFER_SIZE", "160k")
            .WithEnvironment("KONG_NGINX_PROXY_PROXY_BUFFERS", "64 160k")
            .WithBindMount(Path.Combine(dirs.Config, "kong.yml"), "/home/kong/kong.yml", isReadOnly: true)
            .WithHttpEndpoint(port: kongResource.ExternalPort, targetPort: Ports.Kong, name: "http")
            .WaitFor(stack.Auth)
            .WaitFor(stack.Rest)
            .WaitFor(stack.Storage);

        // META (Postgres-Meta)
        var metaResource = new SupabaseMetaResource($"{containerPrefix}-meta") { Stack = stack };

        stack.Meta = builder.AddResource(metaResource)
            .WithImage(Images.PostgresMeta, Images.PostgresMetaTag)
            .WithContainerName($"{containerPrefix}-meta")
            .WithEnvironment("PG_META_PORT", metaResource.Port.ToString())
            .WithEnvironment("PG_META_DB_HOST", $"{containerPrefix}-db")
            .WithEnvironment("PG_META_DB_PORT", Ports.Postgres.ToString())
            .WithEnvironment("PG_META_DB_NAME", "postgres")
            .WithEnvironment("PG_META_DB_USER", "supabase_admin")
            .WithEnvironment("PG_META_DB_PASSWORD", dbResource.Password)
            .WithHttpEndpoint(targetPort: Ports.PostgresMeta, name: "http")
            .WaitFor(stack.Database);

        // STUDIO - Configure the stack resource itself as the Studio container
        stack.StudioPort = Defaults.ExternalStudioPort;

        var stackBuilder = builder.AddResource(stack)
            .WithImage(Images.Studio, Images.StudioTag)
            .WithContainerName(name)
            .WithEnvironment("STUDIO_PG_META_URL", $"http://{containerPrefix}-meta:{Ports.PostgresMeta.ToString()}")
            .WithEnvironment("POSTGRES_PASSWORD", dbResource.Password)
            .WithEnvironment("POSTGRES_HOST", $"{containerPrefix}-db")
            .WithEnvironment("POSTGRES_PORT", Ports.Postgres.ToString())
            .WithEnvironment("POSTGRES_DB", "postgres")
            .WithEnvironment("POSTGRES_USER", "supabase_admin")
            .WithEnvironment("DEFAULT_ORGANIZATION_NAME", "Default Organization")
            .WithEnvironment("DEFAULT_PROJECT_NAME", "Default Project")
            .WithEnvironment("SUPABASE_URL", $"http://{containerPrefix}-kong:{Ports.Kong.ToString()}")
            .WithEnvironment("SUPABASE_PUBLIC_URL", $"http://localhost:{kongResource.ExternalPort.ToString()}")
            .WithEnvironment("SUPABASE_ANON_KEY", stack.AnonKey)
            .WithEnvironment("SUPABASE_SERVICE_KEY", stack.ServiceRoleKey)
            .WithEnvironment("GOTRUE_URL", $"http://{containerPrefix}-auth:{Ports.GoTrue.ToString()}")
            .WithEnvironment("AUTH_JWT_SECRET", stack.JwtSecret)
            .WithEnvironment("LOGFLARE_API_KEY", "")
            .WithEnvironment("LOGFLARE_URL", "")
            .WithEnvironment("NEXT_PUBLIC_ENABLE_LOGS", "false")
            .WithEnvironment("NEXT_ANALYTICS_BACKEND_PROVIDER", "")
            .WithEnvironment("SNIPPETS_MANAGEMENT_FOLDER", "/tmp/snippets")
            .WithHttpEndpoint(port: stack.StudioPort, targetPort: Ports.Studio, name: "http")
            .WaitFor(stack.Meta)
            .WaitFor(stack.Kong)
            .WaitFor(stack.Auth);

        stack.StackBuilder = stackBuilder;

        // Set parent relationships - Stack (Studio) is the visual parent for all containers
        stack.Database.WithParentRelationship(stack);
        stack.Auth.WithParentRelationship(stack);
        stack.Rest.WithParentRelationship(stack);
        stack.Storage.WithParentRelationship(stack);
        stack.Kong.WithParentRelationship(stack);
        stack.Meta.WithParentRelationship(stack);

        // POST-INIT CONTAINER
        var scriptsDir = Path.Combine(Path.GetDirectoryName(dirs.Init)!, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var postInitSqlPath = Path.Combine(scriptsDir, "post_init.sql");
        SupabaseSqlGenerator.WritePostInitSql(postInitSqlPath);

        var postInitShPath = Path.Combine(scriptsDir, "post_init.sh");
        SupabaseSqlGenerator.WritePostInitScript(postInitShPath, $"{containerPrefix}-db", dbResource.Password);

        builder.AddContainer($"{containerPrefix}-init", Images.Postgres, Images.PostgresTag)
            .WithContainerName($"{containerPrefix}-init")
            .WithBindMount(scriptsDir, "/scripts", isReadOnly: true)
            .WithEntrypoint("/bin/bash")
            .WithArgs("/scripts/post_init.sh")
            .WaitFor(stack.Database)
            .WaitFor(stack.Auth)
            .WithParentRelationship(stack);

        return stackBuilder;
    }

    #endregion

    #region Directory Management

    private record SupabaseDirs(string Init, string Storage, string Data, string Config);

    private static SupabaseDirs EnsureDirectories(string root)
    {
        var dirs = new SupabaseDirs(
            Init: Path.Combine(root, "db-init"),
            Storage: Path.Combine(root, "storage"),
            Data: Path.Combine(root, "db-data"),
            Config: Path.Combine(root, "config")
        );
        Directory.CreateDirectory(dirs.Init);
        Directory.CreateDirectory(dirs.Storage);
        Directory.CreateDirectory(dirs.Data);
        Directory.CreateDirectory(dirs.Config);
        return dirs;
    }

    #endregion
}

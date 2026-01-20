using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase.Helpers;
using CommunityToolkit.Aspire.Hosting.Supabase.Resources;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Builders;

/// <summary>
/// Provides extension methods for the SupabaseStackResource.
/// </summary>
public static class SupabaseStackExtensions
{
    #region Edge Functions

    /// <summary>
    /// Configures and creates the Edge Runtime container for Edge Functions.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="functionsPath">The absolute path to the supabase/functions directory.</param>
    public static IResourceBuilder<SupabaseStackResource> WithEdgeFunctions(
        this IResourceBuilder<SupabaseStackResource> builder,
        string functionsPath)
    {
        if (!Directory.Exists(functionsPath))
        {
            Console.WriteLine($"[Supabase] WARNING: Edge Functions directory not found: {functionsPath}");
            return builder;
        }

        var stack = builder.Resource;
        var appBuilder = stack.AppBuilder;

        if (appBuilder == null)
        {
            Console.WriteLine("[Supabase] ERROR: AppBuilder not available. Was AddSupabase() called?");
            return builder;
        }

        // List available functions (only directories with index.ts)
        var functionDirs = Directory.GetDirectories(functionsPath)
            .Select(d => Path.GetFileName(d))
            .Where(name => !name.StartsWith("_") && !name.StartsWith("."))
            .Where(name => File.Exists(Path.Combine(functionsPath, name, "index.ts")))
            .ToList();

        if (functionDirs.Count == 0)
        {
            Console.WriteLine($"[Supabase] WARNING: No Edge Functions with index.ts found in: {functionsPath}");
            return builder;
        }

        Console.WriteLine($"[Supabase] Edge Functions found: {string.Join(", ", functionDirs)}");

        stack.EdgeFunctionsPath = functionsPath;
        var containerPrefix = stack.Name;

        // Create Edge Runtime container
        const int PostgresPort = 5432;
        const int KongPort = 8000;
        const int EdgeRuntimePort = 9000;

        var dbPassword = stack.Database!.Resource.Password;
        var edgeDbUrl = $"postgresql://postgres:{dbPassword}@{containerPrefix}-db:{PostgresPort}/postgres";

        // Generate router file for multi-function support
        var infraRoot = stack.InfraRootDir ?? Path.Combine(appBuilder.AppHostDirectory, "..", "infra", "supabase");
        var edgeDir = Path.Combine(infraRoot, "edge");
        Directory.CreateDirectory(edgeDir);

        var mainTsPath = Path.Combine(edgeDir, "main.ts");
        EdgeFunctionRouter.GenerateRouter(mainTsPath, functionDirs);
        Console.WriteLine($"[Supabase] Edge Router generated: {mainTsPath}");

        // Create the Edge Runtime container with typed resource
        var edgeResource = new SupabaseEdgeRuntimeResource($"{containerPrefix}-edge")
        {
            Port = EdgeRuntimePort,
            FunctionsPath = functionsPath,
            Stack = stack
        };
        edgeResource.FunctionNames.AddRange(functionDirs);

        stack.EdgeRuntime = appBuilder.AddResource(edgeResource)
            .WithImage("denoland/deno", "alpine-2.1.4")
            .WithContainerName($"{containerPrefix}-edge")
            .WithEnvironment("SUPABASE_URL", $"http://{containerPrefix}-kong:{KongPort.ToString()}")
            .WithEnvironment("SUPABASE_ANON_KEY", stack.AnonKey)
            .WithEnvironment("SUPABASE_SERVICE_ROLE_KEY", stack.ServiceRoleKey)
            .WithEnvironment("SUPABASE_DB_URL", edgeDbUrl)
            .WithEnvironment("JWT_SECRET", stack.JwtSecret)
            .WithEnvironment("DENO_DIR", "/tmp/deno")
            .WithEnvironment("EDGE_RUNTIME_PORT", EdgeRuntimePort.ToString())
            .WithBindMount(edgeDir, "/home/deno/main", isReadOnly: true)
            .WithBindMount(functionsPath, "/home/deno/functions", isReadOnly: true)
            .WithHttpEndpoint(targetPort: EdgeRuntimePort, name: "http")
            .WithArgs("run", "--allow-all", "--unstable-worker-options", "/home/deno/main/main.ts")
            .WaitFor(stack.Database!)
            .WaitFor(stack.Kong!);

        // Set parent relationship to the stack (which IS the Studio)
        stack.EdgeRuntime.WithParentRelationship(stack);

        Console.WriteLine($"[Supabase] Edge Runtime container created with {functionDirs.Count} functions");
        return builder;
    }

    #endregion

    #region Migrations

    /// <summary>
    /// Applies database migrations from SQL files in the specified directory.
    /// Migrations are executed in alphabetical order by filename AFTER GoTrue starts.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="migrationsPath">The absolute path to the supabase/migrations directory.</param>
    public static IResourceBuilder<SupabaseStackResource> WithMigrations(
        this IResourceBuilder<SupabaseStackResource> builder,
        string migrationsPath)
    {
        if (!Directory.Exists(migrationsPath))
        {
            Console.WriteLine($"[Supabase] WARNING: Migrations directory not found: {migrationsPath}");
            return builder;
        }

        var stack = builder.Resource;

        if (stack.InitSqlPath == null)
        {
            Console.WriteLine("[Supabase] ERROR: InitSqlPath not set. Was AddSupabase() called?");
            return builder;
        }

        // Find all SQL files and sort by name
        var sqlFiles = Directory.GetFiles(migrationsPath, "*.sql")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        if (sqlFiles.Count == 0)
        {
            Console.WriteLine($"[Supabase] No migrations found in: {migrationsPath}");
            return builder;
        }

        Console.WriteLine($"[Supabase] {sqlFiles.Count} migrations found");

        // Create combined migrations file
        var combinedSql = new System.Text.StringBuilder();
        combinedSql.AppendLine("-- ============================================");
        combinedSql.AppendLine("-- SUPABASE MIGRATIONS (auto-generated)");
        combinedSql.AppendLine($"-- Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        combinedSql.AppendLine($"-- Source: {migrationsPath}");
        combinedSql.AppendLine("-- ============================================");
        combinedSql.AppendLine();
        combinedSql.AppendLine("-- Wait for auth.users table (GoTrue must start first)");
        combinedSql.AppendLine("DO $$");
        combinedSql.AppendLine("DECLARE");
        combinedSql.AppendLine("    retry_count integer := 0;");
        combinedSql.AppendLine("    max_retries integer := 30;");
        combinedSql.AppendLine("BEGIN");
        combinedSql.AppendLine("    WHILE NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'auth' AND tablename = 'users') AND retry_count < max_retries LOOP");
        combinedSql.AppendLine("        PERFORM pg_sleep(1);");
        combinedSql.AppendLine("        retry_count := retry_count + 1;");
        combinedSql.AppendLine("        RAISE NOTICE '[Migrations] Waiting for auth.users... (Attempt %/%)', retry_count, max_retries;");
        combinedSql.AppendLine("    END LOOP;");
        combinedSql.AppendLine("    IF NOT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'auth' AND tablename = 'users') THEN");
        combinedSql.AppendLine("        RAISE EXCEPTION '[Migrations] auth.users was not found after % attempts', max_retries;");
        combinedSql.AppendLine("    END IF;");
        combinedSql.AppendLine("    RAISE NOTICE '[Migrations] auth.users found, starting migrations';");
        combinedSql.AppendLine("END;");
        combinedSql.AppendLine("$$;");
        combinedSql.AppendLine();

        foreach (var sqlFile in sqlFiles)
        {
            var fileName = Path.GetFileName(sqlFile);
            combinedSql.AppendLine($"-- Migration: {fileName}");
            combinedSql.AppendLine($"-- ----------------------------------------");

            try
            {
                var content = File.ReadAllText(sqlFile);
                combinedSql.AppendLine(content);
                combinedSql.AppendLine();
                Console.WriteLine($"[Supabase]   + {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase] WARNING: Could not read {fileName}: {ex.Message}");
            }
        }

        combinedSql.AppendLine("-- ============================================");
        combinedSql.AppendLine("-- END MIGRATIONS");
        combinedSql.AppendLine("-- ============================================");

        // Write to scripts directory (executed by post_init.sh, AFTER GoTrue start)
        var scriptsDir = Path.Combine(Path.GetDirectoryName(stack.InitSqlPath)!, "scripts");
        Directory.CreateDirectory(scriptsDir);
        var migrationsOutputPath = Path.Combine(scriptsDir, "migrations.sql");
        File.WriteAllText(migrationsOutputPath, combinedSql.ToString());

        Console.WriteLine($"[Supabase] Migrations written to: {migrationsOutputPath}");
        return builder;
    }

    #endregion

    #region User Registration

    /// <summary>
    /// Registers a development user that will be created on startup.
    /// The user will have a profile and admin role automatically.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="password">The user's password.</param>
    /// <param name="displayName">Optional display name (defaults to email).</param>
    public static IResourceBuilder<SupabaseStackResource> WithRegisteredUser(
        this IResourceBuilder<SupabaseStackResource> builder,
        string email,
        string password,
        string? displayName = null)
    {
        var user = new RegisteredUser(email, password, displayName ?? email);
        builder.Resource.RegisteredUsers.Add(user);

        var scriptsDir = builder.Resource.InitSqlPath != null
            ? Path.Combine(Path.GetDirectoryName(builder.Resource.InitSqlPath)!, "scripts")
            : null;

        if (scriptsDir != null)
        {
            Directory.CreateDirectory(scriptsDir);
            var userSqlPath = Path.Combine(scriptsDir, "users.sql");
            AppendUserSql(userSqlPath, user);
            Console.WriteLine($"[Supabase] User registered: {email} -> {userSqlPath}");
        }
        else
        {
            Console.WriteLine($"[Supabase] WARNING: InitSqlPath is null, user SQL cannot be created!");
        }

        return builder;
    }

    private static void AppendUserSql(string path, RegisteredUser user)
    {
        var email = user.Email.Replace("'", "''");
        var displayName = user.DisplayName.Replace("'", "''");
        var password = user.Password.Replace("'", "''");

        var appMetaData = @"{""provider"": ""email"", ""providers"": [""email""]}";
        var userMetaData = @"{""display_name"": """ + displayName + @"""}";

        var sql = $"""
-- User: {user.Email}
DO $$
DECLARE
    new_user_id uuid;
    hashed_password text;
BEGIN
    -- Check if user already exists
    SELECT id INTO new_user_id FROM auth.users WHERE email = '{email}';

    IF new_user_id IS NULL THEN
        -- Hash password
        hashed_password := extensions.crypt('{password}', extensions.gen_salt('bf', 10));

        -- Create user in auth.users
        INSERT INTO auth.users (
            instance_id, id, aud, role, email, encrypted_password,
            email_confirmed_at, raw_app_meta_data, raw_user_meta_data,
            created_at, updated_at, confirmation_token, email_change,
            email_change_token_new, recovery_token
        ) VALUES (
            '00000000-0000-0000-0000-000000000000',
            extensions.uuid_generate_v4(),
            'authenticated', 'authenticated', '{email}', hashed_password,
            NOW(), '{appMetaData}'::jsonb, '{userMetaData}'::jsonb,
            NOW(), NOW(), '', '', '', ''
        )
        RETURNING id INTO new_user_id;

        RAISE NOTICE '[Post-Init] User created: {email} (ID: %)', new_user_id;

        -- Create profile (with exception handling)
        BEGIN
            IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'profiles') THEN
                IF NOT EXISTS (SELECT 1 FROM public.profiles WHERE user_id = new_user_id) THEN
                    INSERT INTO public.profiles (user_id, email, display_name, is_disabled, created_at, updated_at)
                    VALUES (new_user_id, '{email}', '{displayName}', false, NOW(), NOW());
                END IF;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING '[Post-Init] Profile creation failed for {email}: %', SQLERRM;
        END;

        -- Create admin role (with exception handling)
        BEGIN
            IF EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'user_roles') THEN
                IF NOT EXISTS (SELECT 1 FROM public.user_roles WHERE user_id = new_user_id) THEN
                    INSERT INTO public.user_roles (user_id, role, created_at)
                    VALUES (new_user_id, 'admin', NOW());
                END IF;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RAISE WARNING '[Post-Init] Role creation failed for {email}: %', SQLERRM;
        END;
    ELSE
        RAISE NOTICE '[Post-Init] User already exists: {email}';
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE WARNING '[Post-Init] User creation completely failed for {email}: %', SQLERRM;
END;
$$;

""";
        File.AppendAllText(path, sql);
    }

    #endregion

    #region Clear Command

    /// <summary>
    /// Adds a "Clear All Data" command to the Kong container in the Aspire dashboard.
    /// This stops all Supabase containers and deletes all data for a fresh start.
    /// </summary>
    public static IResourceBuilder<SupabaseStackResource> WithClearCommand(this IResourceBuilder<SupabaseStackResource> builder)
    {
        var containerPrefix = builder.Resource.Name;
        var infraPath = builder.Resource.InitSqlPath != null
            ? Path.GetDirectoryName(Path.GetDirectoryName(builder.Resource.InitSqlPath))
            : null;
        CommandOptions options = new()
        {
            IconName = "Delete",
            IconVariant = IconVariant.Filled,
            UpdateState = context => ResourceCommandState.Enabled
        };
        builder.WithCommand(
            name: "clear-supabase",
            displayName: "Clear All Supabase Data",
            executeCommand: _ =>
            {
                Console.WriteLine("[Supabase Clear] Deleting all data...");

                var containerNames = new[]
                {
                        $"{containerPrefix}-db",
                        $"{containerPrefix}-auth",
                        $"{containerPrefix}-rest",
                        $"{containerPrefix}-storage",
                        $"{containerPrefix}-kong",
                        $"{containerPrefix}-meta",
                        $"{containerPrefix}-studio",
                        $"{containerPrefix}-edge",
                        $"{containerPrefix}-init"
                };

                foreach (var container in containerNames)
                {
                    try
                    {
                        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "docker",
                            Arguments = $"rm -f {container}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        process?.WaitForExit(10000);
                        Console.WriteLine($"[Supabase Clear] Container removed: {container}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Supabase Clear] WARNING: {container} - {ex.Message}");
                    }
                }

                if (!string.IsNullOrEmpty(infraPath) && Directory.Exists(infraPath))
                {
                    try
                    {
                        Directory.Delete(infraPath, recursive: true);
                        Console.WriteLine($"[Supabase Clear] Directory deleted: {infraPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Supabase Clear] WARNING: {ex.Message}");
                    }
                }

                Console.WriteLine("[Supabase Clear] Cleanup completed. Please restart Aspire.");
                return Task.FromResult(new ExecuteCommandResult() { Success = true});
            }, options
            );

        return builder;
    }

    #endregion

    #region Getters

    /// <summary>
    /// Gets the Kong API Gateway container resource.
    /// </summary>
    public static IResourceBuilder<ContainerResource>? GetKong(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.Kong;

    /// <summary>
    /// Gets the PostgreSQL Database container resource.
    /// </summary>
    public static IResourceBuilder<ContainerResource>? GetDatabase(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.Database;

    /// <summary>
    /// Gets the Auth (GoTrue) container resource.
    /// </summary>
    public static IResourceBuilder<ContainerResource>? GetAuth(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.Auth;

    /// <summary>
    /// Gets the REST (PostgREST) container resource.
    /// </summary>
    public static IResourceBuilder<ContainerResource>? GetRest(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.Rest;

    /// <summary>
    /// Gets the Storage API container resource.
    /// </summary>
    public static IResourceBuilder<ContainerResource>? GetStorage(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.Storage;

    /// <summary>
    /// Gets the Postgres-Meta container resource.
    /// </summary>
    public static IResourceBuilder<ContainerResource>? GetMeta(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.Meta;

    /// <summary>
    /// Gets the Anon Key for client-side authentication.
    /// </summary>
    public static string GetAnonKey(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.AnonKey;

    /// <summary>
    /// Gets the Service Role Key for server-side authentication.
    /// </summary>
    public static string GetServiceRoleKey(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.ServiceRoleKey;

    /// <summary>
    /// Gets the API URL for environment variable injection.
    /// </summary>
    public static string GetApiUrl(this IResourceBuilder<SupabaseStackResource> builder)
        => builder.Resource.GetApiUrl();

    #endregion

    #region JWT Configuration

    /// <summary>
    /// Configures the JWT secret used for token signing.
    /// </summary>
    public static IResourceBuilder<SupabaseStackResource> WithJwtSecret(
        this IResourceBuilder<SupabaseStackResource> builder,
        string secret)
    {
        builder.Resource.JwtSecret = secret;
        return builder;
    }

    /// <summary>
    /// Configures the anonymous key for public API access.
    /// </summary>
    public static IResourceBuilder<SupabaseStackResource> WithAnonKey(
        this IResourceBuilder<SupabaseStackResource> builder,
        string anonKey)
    {
        builder.Resource.AnonKey = anonKey;
        return builder;
    }

    /// <summary>
    /// Configures the service role key for admin API access.
    /// </summary>
    public static IResourceBuilder<SupabaseStackResource> WithServiceRoleKey(
        this IResourceBuilder<SupabaseStackResource> builder,
        string serviceRoleKey)
    {
        builder.Resource.ServiceRoleKey = serviceRoleKey;
        return builder;
    }

    #endregion
}

using CommunityToolkit.Aspire.Hosting.Supabase.Helpers;
using System.Text;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Supabase.Sync;

/// <summary>
/// Service for synchronizing data from an online Supabase project to the local development environment.
/// </summary>
internal static class SyncService
{
    /// <summary>
    /// Synchronizes schema, data, storage, and edge functions from an online Supabase project.
    /// </summary>
    public static async Task SyncFromOnlineProject(
        string initPath,
        string projectRef,
        string serviceKey,
        SyncOptions options,
        string? dbPassword,
        string? storagePath,
        string? managementApiToken = null,
        string? edgeFunctionsPath = null)
    {
        Console.WriteLine($"[Supabase Sync] Synchronizing from project: {projectRef}");
        Console.WriteLine($"[Supabase Sync] Options: {options}");

        var baseUrl = $"https://{projectRef}.supabase.co";
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("apikey", serviceKey);

        var sqlBuilder = new StringBuilder();
        sqlBuilder.AppendLine("-- ============================================");
        sqlBuilder.AppendLine($"-- SYNCED FROM ONLINE PROJECT: {projectRef}");
        sqlBuilder.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sqlBuilder.AppendLine("-- ============================================");
        sqlBuilder.AppendLine();

        var schemaCreated = false;
        if (options.HasFlag(SyncOptions.Schema))
        {
            schemaCreated = await SyncSchema(httpClient, baseUrl, sqlBuilder);
        }

        bool hasPgDumpOptions = !string.IsNullOrWhiteSpace(dbPassword) &&
            (options.HasFlag(SyncOptions.Policies) ||
             options.HasFlag(SyncOptions.Functions) ||
             options.HasFlag(SyncOptions.Triggers) ||
             options.HasFlag(SyncOptions.Types) ||
             options.HasFlag(SyncOptions.Views) ||
             options.HasFlag(SyncOptions.Indexes));

        if (hasPgDumpOptions)
        {
            await SyncWithPgDump(initPath, projectRef, dbPassword!, options, sqlBuilder);
        }

        if (options.HasFlag(SyncOptions.Data))
        {
            if (!schemaCreated)
            {
                Console.WriteLine("[Supabase Sync] WARNING: Data sync skipped - schema was not created!");
            }
            else
            {
                await SyncData(httpClient, baseUrl, sqlBuilder);
            }
        }

        if (options.HasFlag(SyncOptions.StorageBuckets) || options.HasFlag(SyncOptions.StorageFiles))
        {
            var (bucketsSql, objectsSql) = await SyncStorageComplete(
                baseUrl,
                serviceKey,
                storagePath,
                options.HasFlag(SyncOptions.StorageBuckets),
                options.HasFlag(SyncOptions.StorageFiles));

            if (!string.IsNullOrEmpty(bucketsSql))
            {
                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine("-- === STORAGE BUCKETS ===");
                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine(bucketsSql);
            }

            if (!string.IsNullOrEmpty(objectsSql))
            {
                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine("-- === STORAGE OBJECTS ===");
                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine(objectsSql);
            }
        }

        if (options.HasFlag(SyncOptions.EdgeFunctions) &&
            !string.IsNullOrWhiteSpace(managementApiToken) &&
            !string.IsNullOrWhiteSpace(edgeFunctionsPath))
        {
            await SyncEdgeFunctions(projectRef, managementApiToken, edgeFunctionsPath);
        }

        var syncSqlPath = Path.Combine(initPath, "01_sync_schema.sql");
        await File.WriteAllTextAsync(syncSqlPath, sqlBuilder.ToString());
        Console.WriteLine($"[Supabase Sync] Sync saved to: {syncSqlPath}");
    }

    private static async Task<bool> SyncSchema(HttpClient httpClient, string baseUrl, StringBuilder sqlBuilder)
    {
        try
        {
            Console.WriteLine("[Supabase Sync] Loading OpenAPI specification for schema...");
            var openApiResponse = await httpClient.GetStringAsync($"{baseUrl}/rest/v1/");
            var openApi = JsonSerializer.Deserialize<JsonElement>(openApiResponse);

            if (!openApi.TryGetProperty("definitions", out var definitions))
            {
                Console.WriteLine("[Supabase Sync] No table definitions found.");
                return false;
            }

            var customTypes = new HashSet<string>();
            sqlBuilder.AppendLine("-- === TABLES (from OpenAPI spec) ===");
            sqlBuilder.AppendLine();

            foreach (var tableDef in definitions.EnumerateObject())
            {
                var tableName = tableDef.Name;
                var tableSchema = tableDef.Value;

                if (tableName.StartsWith("_") || tableName == "rpc") continue;

                Console.WriteLine($"[Supabase Sync] Synchronizing table: public.{tableName}");

                if (!tableSchema.TryGetProperty("properties", out var properties))
                    continue;

                sqlBuilder.AppendLine($"-- Table: {tableName}");
                sqlBuilder.AppendLine($"CREATE TABLE IF NOT EXISTS public.{tableName} (");

                var columnDefs = new List<string>();
                var primaryKeys = new List<string>();

                var requiredFields = new HashSet<string>();
                if (tableSchema.TryGetProperty("required", out var required))
                {
                    foreach (var req in required.EnumerateArray())
                        requiredFields.Add(req.GetString() ?? "");
                }

                foreach (var prop in properties.EnumerateObject())
                {
                    var colName = prop.Name;
                    var colDef = prop.Value;
                    var pgType = MapOpenApiToPostgres(colDef, customTypes);
                    var isNullable = !requiredFields.Contains(colName);
                    var isPrimaryKey = colName == "id" ||
                                      (colDef.TryGetProperty("description", out var desc) &&
                                       desc.GetString()?.Contains("Primary") == true);

                    var colLine = $"    \"{colName}\" {pgType}";
                    if (!isNullable) colLine += " NOT NULL";
                    if (pgType == "uuid" && isPrimaryKey)
                        colLine += " DEFAULT extensions.uuid_generate_v4()";

                    columnDefs.Add(colLine);
                    if (isPrimaryKey) primaryKeys.Add($"\"{colName}\"");
                }

                if (primaryKeys.Count > 0)
                    columnDefs.Add($"    PRIMARY KEY ({string.Join(", ", primaryKeys)})");

                sqlBuilder.AppendLine(string.Join(",\n", columnDefs));
                sqlBuilder.AppendLine(");");
                sqlBuilder.AppendLine($"ALTER TABLE public.{tableName} ENABLE ROW LEVEL SECURITY;");
                sqlBuilder.AppendLine($"DROP POLICY IF EXISTS \"Allow all for development\" ON public.{tableName};");
                sqlBuilder.AppendLine($"CREATE POLICY \"Allow all for development\" ON public.{tableName} FOR ALL USING (true) WITH CHECK (true);");
                sqlBuilder.AppendLine();
            }

            if (customTypes.Count > 0)
            {
                Console.WriteLine($"[Supabase Sync] WARNING: {customTypes.Count} custom types were replaced with TEXT:");
                foreach (var ct in customTypes)
                    Console.WriteLine($"  - {ct}");
            }

            Console.WriteLine("[Supabase Sync] Schema sync completed.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Sync] Error during schema sync: {ex.Message}");
            sqlBuilder.AppendLine($"-- Error during schema sync: {ex.Message}");
            return false;
        }
    }

    private static async Task SyncData(HttpClient httpClient, string baseUrl, StringBuilder sqlBuilder)
    {
        try
        {
            Console.WriteLine("[Supabase Sync] Loading data...");

            var openApiResponse = await httpClient.GetStringAsync($"{baseUrl}/rest/v1/");
            var openApi = JsonSerializer.Deserialize<JsonElement>(openApiResponse);

            if (openApi.TryGetProperty("definitions", out var definitions))
            {
                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine("-- === DATA ===");
                sqlBuilder.AppendLine();

                foreach (var tableDef in definitions.EnumerateObject())
                {
                    var tableName = tableDef.Name;
                    if (tableName.StartsWith("_") || tableName == "rpc") continue;

                    try
                    {
                        httpClient.DefaultRequestHeaders.Remove("Prefer");
                        httpClient.DefaultRequestHeaders.Add("Prefer", "return=representation");

                        var dataResponse = await httpClient.GetStringAsync($"{baseUrl}/rest/v1/{tableName}?limit=1000");
                        var rows = JsonSerializer.Deserialize<JsonElement>(dataResponse);

                        if (rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() > 0)
                        {
                            Console.WriteLine($"[Supabase Sync] Synchronizing {rows.GetArrayLength()} rows from: {tableName}");
                            sqlBuilder.AppendLine($"-- Data for: {tableName}");

                            foreach (var row in rows.EnumerateArray())
                            {
                                var columns = new List<string>();
                                var values = new List<string>();

                                foreach (var prop in row.EnumerateObject())
                                {
                                    columns.Add($"\"{prop.Name}\"");
                                    values.Add(JsonValueToSql(prop.Value));
                                }

                                sqlBuilder.AppendLine($"INSERT INTO public.{tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)}) ON CONFLICT DO NOTHING;");
                            }
                            sqlBuilder.AppendLine();
                        }
                    }
                    catch (HttpRequestException)
                    {
                        // Table might not be accessible, skip it
                    }
                }
            }

            Console.WriteLine("[Supabase Sync] Data sync completed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Sync] Error during data sync: {ex.Message}");
            sqlBuilder.AppendLine($"-- Error during data sync: {ex.Message}");
        }
    }

    private static async Task<(string bucketsSql, string objectsSql)> SyncStorageComplete(
        string baseUrl,
        string serviceKey,
        string? storagePath,
        bool syncBuckets,
        bool syncFiles)
    {
        var bucketsSqlBuilder = new StringBuilder();
        var objectsSqlBuilder = new StringBuilder();

        try
        {
            Console.WriteLine("[Supabase Sync] Loading storage data...");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("apikey", serviceKey);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");

            var bucketsResponse = await client.GetStringAsync($"{baseUrl}/storage/v1/bucket");
            var buckets = JsonSerializer.Deserialize<JsonElement>(bucketsResponse);

            if (buckets.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("[Supabase Sync] No buckets found.");
                return ("", "");
            }

            if (syncBuckets)
            {
                foreach (var bucket in buckets.EnumerateArray())
                {
                    var id = bucket.GetProperty("id").GetString();
                    var name = bucket.GetProperty("name").GetString();
                    var isPublic = bucket.TryGetProperty("public", out var pub) && pub.GetBoolean();

                    Console.WriteLine($"[Supabase Sync] Storage bucket: {name} (public: {isPublic})");
                    bucketsSqlBuilder.AppendLine($"INSERT INTO storage.buckets (id, name, public, created_at, updated_at) VALUES ('{SupabaseSqlGenerator.EscapeSqlString(id!)}', '{SupabaseSqlGenerator.EscapeSqlString(name!)}', {isPublic.ToString().ToLower()}, NOW(), NOW()) ON CONFLICT (id) DO NOTHING;");
                }
                Console.WriteLine("[Supabase Sync] Storage bucket sync completed.");
            }

            if (syncFiles && !string.IsNullOrEmpty(storagePath))
            {
                var totalFiles = 0;
                foreach (var bucket in buckets.EnumerateArray())
                {
                    var bucketId = bucket.GetProperty("id").GetString();
                    if (string.IsNullOrEmpty(bucketId)) continue;

                    var bucketPath = Path.Combine(storagePath, bucketId);
                    Directory.CreateDirectory(bucketPath);

                    try
                    {
                        var (filesDownloaded, objectsSql) = await SyncBucketFiles(baseUrl, serviceKey, bucketId, "", bucketPath);
                        totalFiles += filesDownloaded;
                        objectsSqlBuilder.Append(objectsSql);

                        if (filesDownloaded > 0)
                            Console.WriteLine($"[Supabase Sync] {filesDownloaded} files downloaded from bucket '{bucketId}'.");
                        else
                            Console.WriteLine($"[Supabase Sync] Bucket '{bucketId}' is empty or not accessible.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Supabase Sync] Error with bucket '{bucketId}': {ex.Message}");
                    }
                }

                Console.WriteLine($"[Supabase Sync] Storage files sync completed. {totalFiles} files total.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Sync] Error during storage sync: {ex.Message}");
        }

        return (bucketsSqlBuilder.ToString(), objectsSqlBuilder.ToString());
    }

    private static async Task<(int fileCount, string sql)> SyncBucketFiles(string baseUrl, string serviceKey, string bucketId, string prefix, string localPath)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("apikey", serviceKey);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {serviceKey}");

        var fileCount = 0;
        var sqlBuilder = new StringBuilder();

        try
        {
            var listUrl = $"{baseUrl}/storage/v1/object/list/{bucketId}";
            var listRequest = new HttpRequestMessage(HttpMethod.Post, listUrl);
            listRequest.Headers.Add("apikey", serviceKey);
            listRequest.Headers.Add("Authorization", $"Bearer {serviceKey}");
            listRequest.Content = new StringContent(
                JsonSerializer.Serialize(new { prefix, limit = 1000 }),
                Encoding.UTF8,
                "application/json");

            var listResponse = await client.SendAsync(listRequest);

            if (!listResponse.IsSuccessStatusCode)
            {
                var errorContent = await listResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[Supabase Sync] Error listing {bucketId}/{prefix}: {listResponse.StatusCode} - {errorContent}");
                return (0, "");
            }

            var filesJson = await listResponse.Content.ReadAsStringAsync();
            var files = JsonSerializer.Deserialize<JsonElement>(filesJson);

            if (files.ValueKind != JsonValueKind.Array) return (0, "");

            foreach (var file in files.EnumerateArray())
            {
                var fileName = file.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                if (string.IsNullOrEmpty(fileName)) continue;

                var isFolder = file.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Null;

                if (isFolder)
                {
                    var subPrefix = string.IsNullOrEmpty(prefix) ? fileName : $"{prefix}/{fileName}";
                    var subPath = Path.Combine(localPath, fileName);
                    Directory.CreateDirectory(subPath);
                    var (subCount, subSql) = await SyncBucketFiles(baseUrl, serviceKey, bucketId, subPrefix, subPath);
                    fileCount += subCount;
                    sqlBuilder.Append(subSql);
                }
                else
                {
                    try
                    {
                        var fullPath = string.IsNullOrEmpty(prefix) ? fileName : $"{prefix}/{fileName}";
                        var fileUrl = $"{baseUrl}/storage/v1/object/{bucketId}/{Uri.EscapeDataString(fullPath)}";

                        var downloadRequest = new HttpRequestMessage(HttpMethod.Get, fileUrl);
                        downloadRequest.Headers.Add("apikey", serviceKey);
                        downloadRequest.Headers.Add("Authorization", $"Bearer {serviceKey}");

                        var downloadResponse = await client.SendAsync(downloadRequest);
                        if (downloadResponse.IsSuccessStatusCode)
                        {
                            var fileBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
                            var localFilePath = Path.Combine(localPath, fileName);

                            var fileDir = Path.GetDirectoryName(localFilePath);
                            if (!string.IsNullOrEmpty(fileDir))
                                Directory.CreateDirectory(fileDir);

                            await File.WriteAllBytesAsync(localFilePath, fileBytes);
                            fileCount++;

                            var fileId = file.TryGetProperty("id", out var fid) && fid.ValueKind == JsonValueKind.String
                                ? fid.GetString()
                                : Guid.NewGuid().ToString();
                            var mimeType = file.TryGetProperty("metadata", out var meta) &&
                                          meta.TryGetProperty("mimetype", out var mime)
                                ? mime.GetString() ?? "application/octet-stream"
                                : "application/octet-stream";

                            sqlBuilder.AppendLine($"INSERT INTO storage.objects (id, bucket_id, name, metadata, created_at, updated_at) " +
                                $"VALUES ('{SupabaseSqlGenerator.EscapeSqlString(fileId!)}', '{SupabaseSqlGenerator.EscapeSqlString(bucketId)}', '{SupabaseSqlGenerator.EscapeSqlString(fullPath)}', " +
                                $"'{{\"mimetype\": \"{SupabaseSqlGenerator.EscapeSqlString(mimeType)}\", \"size\": {fileBytes.Length}}}'::jsonb, NOW(), NOW()) " +
                                $"ON CONFLICT (id) DO NOTHING;");
                        }
                        else
                        {
                            Console.WriteLine($"[Supabase Sync] Error downloading {bucketId}/{fullPath}: {downloadResponse.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Supabase Sync] Error downloading {bucketId}/{fileName}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Sync] Error listing {bucketId}/{prefix}: {ex.Message}");
        }

        return (fileCount, sqlBuilder.ToString());
    }

    private static async Task SyncWithPgDump(string initPath, string projectRef, string dbPassword, SyncOptions options, StringBuilder sqlBuilder)
    {
        Console.WriteLine("[Supabase Sync] Starting pg_dump for complete schema sync...");

        var connectionString = $"postgresql://postgres.{projectRef}:{Uri.EscapeDataString(dbPassword)}@aws-0-eu-central-1.pooler.supabase.com:6543/postgres";

        var pgDumpPath = FindPgDump();
        if (string.IsNullOrEmpty(pgDumpPath))
        {
            Console.WriteLine("[Supabase Sync] WARNING: pg_dump not found. Skipping complete schema sync.");
            Console.WriteLine("                Install PostgreSQL client tools for complete sync.");
            return;
        }

        var args = new List<string>
        {
            $"\"{connectionString}\"",
            "--schema-only",
            "--no-owner",
            "--no-acl",
            "--no-comments"
        };

        args.Add("--schema=public");

        if (!options.HasFlag(SyncOptions.Schema))
            args.Add("--exclude-table=*");

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pgDumpPath,
                Arguments = string.Join(" ", args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["PGPASSWORD"] = dbPassword }
            };

            Console.WriteLine($"[Supabase Sync] Executing: {pgDumpPath}");

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine("[Supabase Sync] ERROR: pg_dump could not be started.");
                return;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"[Supabase Sync] pg_dump error (Exit {process.ExitCode}): {errors}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(output))
            {
                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine("-- === SCHEMA FROM PG_DUMP ===");
                sqlBuilder.AppendLine();

                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("--") && !line.Contains("Table:") && !line.Contains("Function:") && !line.Contains("Policy:"))
                        continue;

                    if (line.Contains("supabase_functions") || line.Contains("supabase_migrations"))
                        continue;

                    sqlBuilder.AppendLine(line);
                }

                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine("-- === DEV-MODE RLS POLICIES ===");
                sqlBuilder.AppendLine();
                sqlBuilder.AppendLine(@"
DO $$
DECLARE
    t record;
BEGIN
    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'public'
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS ""Allow all for development"" ON public.%I', t.tablename);
        EXECUTE format('CREATE POLICY ""Allow all for development"" ON public.%I FOR ALL USING (true) WITH CHECK (true)', t.tablename);
    END LOOP;
END;
$$;
");

                Console.WriteLine("[Supabase Sync] pg_dump schema sync completed.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Sync] pg_dump error: {ex.Message}");
        }
    }

    private static string? FindPgDump()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);

        var pgDumpNames = OperatingSystem.IsWindows()
            ? new[] { "pg_dump.exe" }
            : new[] { "pg_dump" };

        foreach (var path in paths)
        {
            foreach (var name in pgDumpNames)
            {
                var fullPath = Path.Combine(path, name);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        var standardPaths = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            for (var ver = 17; ver >= 12; ver--)
            {
                standardPaths.Add($@"C:\Program Files\PostgreSQL\{ver}\bin\pg_dump.exe");
                standardPaths.Add($@"C:\Program Files (x86)\PostgreSQL\{ver}\bin\pg_dump.exe");
            }
        }
        else
        {
            standardPaths.Add("/usr/bin/pg_dump");
            standardPaths.Add("/usr/local/bin/pg_dump");
            standardPaths.Add("/opt/homebrew/bin/pg_dump");
        }

        foreach (var path in standardPaths)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static async Task SyncEdgeFunctions(string projectRef, string managementApiToken, string edgeFunctionsPath)
    {
        Console.WriteLine("[Supabase Sync] Starting Edge Functions sync...");

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {managementApiToken}");

            var functionsUrl = $"https://api.supabase.com/v1/projects/{projectRef}/functions";
            var response = await client.GetAsync(functionsUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Supabase Sync] Error fetching Edge Functions: {response.StatusCode}");
                Console.WriteLine($"                {errorContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("                Note: The Management API token is invalid or expired.");
                    Console.WriteLine("                Create a new token at: Dashboard → Account → Access Tokens");
                }

                return;
            }

            var functionsJson = await response.Content.ReadAsStringAsync();
            var functions = JsonSerializer.Deserialize<JsonElement>(functionsJson);

            if (functions.ValueKind != JsonValueKind.Array || functions.GetArrayLength() == 0)
            {
                Console.WriteLine("[Supabase Sync] No Edge Functions found in project.");
                return;
            }

            Directory.CreateDirectory(edgeFunctionsPath);

            var syncedCount = 0;
            foreach (var func in functions.EnumerateArray())
            {
                var slug = func.TryGetProperty("slug", out var slugProp) ? slugProp.GetString() : null;
                var name = func.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : slug;
                var status = func.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "unknown";

                if (string.IsNullOrEmpty(slug))
                    continue;

                Console.WriteLine($"[Supabase Sync] Edge Function: {name} ({slug}) - Status: {status}");

                var functionDir = Path.Combine(edgeFunctionsPath, slug);
                Directory.CreateDirectory(functionDir);

                var functionDetailUrl = $"https://api.supabase.com/v1/projects/{projectRef}/functions/{slug}/body";
                var bodyResponse = await client.GetAsync(functionDetailUrl);

                string functionCode = "";
                var useSourceCode = false;

                if (bodyResponse.IsSuccessStatusCode)
                {
                    var bodyBytes = await bodyResponse.Content.ReadAsByteArrayAsync();
                    var bodyText = Encoding.UTF8.GetString(bodyBytes);

                    if (bodyBytes.Length > 5 &&
                        bodyBytes[0] == 'E' && bodyBytes[1] == 'S' && bodyBytes[2] == 'Z' && bodyBytes[3] == 'I' && bodyBytes[4] == 'P')
                    {
                        Console.WriteLine($"[Supabase Sync]   → ESZIP bundle detected (compiled, not usable as source)");
                    }
                    else if (bodyText.Contains("import") || bodyText.Contains("export") || bodyText.Contains("Deno.serve") || bodyText.Contains("serve("))
                    {
                        functionCode = bodyText;
                        useSourceCode = true;
                        Console.WriteLine($"[Supabase Sync]   → Source code downloaded");
                    }
                }

                if (!useSourceCode)
                {
                    functionCode = "// Edge Function: " + name + "\n" +
                        "// Slug: " + slug + "\n" +
                        "// Status: " + status + "\n" +
                        "//\n" +
                        "// ⚠️ IMPORTANT: The Supabase API only returns compiled ESZIP bundles,\n" +
                        "// not the original source code. You must copy the code manually!\n" +
                        "//\n" +
                        "// Option 1 - Copy from Dashboard:\n" +
                        "// https://supabase.com/dashboard/project/" + projectRef + "/functions/" + slug + "\n" +
                        "//\n" +
                        "// Option 2 - Download with Supabase CLI:\n" +
                        "// supabase login\n" +
                        "// supabase functions download " + slug + " --project-ref " + projectRef + "\n" +
                        "//\n" +
                        "// Replace this placeholder with the actual code!\n\n" +
                        "import { serve } from \"https://deno.land/std@0.177.0/http/server.ts\";\n\n" +
                        "serve(async (req) => {\n" +
                        "  return new Response(\n" +
                        "    JSON.stringify({ error: \"Function not synced - see comments in source\" }),\n" +
                        "    { status: 501, headers: { \"Content-Type\": \"application/json\" } }\n" +
                        "  );\n" +
                        "});\n";
                    Console.WriteLine($"[Supabase Sync]   → Placeholder created (copy source code manually!)");
                }

                var indexPath = Path.Combine(functionDir, "index.ts");
                await File.WriteAllTextAsync(indexPath, functionCode);

                syncedCount++;
            }

            if (syncedCount > 0)
            {
                Console.WriteLine($"[Supabase Sync] {syncedCount} Edge Function(s) synchronized to: {edgeFunctionsPath}");
                Console.WriteLine("[Supabase Sync] NOTE: Check the synchronized functions for completeness.");
                Console.WriteLine("                If the source code is missing, copy it manually from the dashboard.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Supabase Sync] Error during Edge Functions sync: {ex.Message}");
        }
    }

    private static string JsonValueToSql(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null => "NULL",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => $"'{SupabaseSqlGenerator.EscapeSqlString(value.GetString() ?? "")}'",
            JsonValueKind.Array => $"'{SupabaseSqlGenerator.EscapeSqlString(value.GetRawText())}'::jsonb",
            JsonValueKind.Object => $"'{SupabaseSqlGenerator.EscapeSqlString(value.GetRawText())}'::jsonb",
            _ => "NULL"
        };
    }

    private static string MapOpenApiToPostgres(JsonElement colDef, HashSet<string> customTypes)
    {
        var type = colDef.TryGetProperty("type", out var t) ? t.GetString() : "string";
        var format = colDef.TryGetProperty("format", out var f) ? f.GetString() : null;

        if (!string.IsNullOrEmpty(format))
        {
            if (format.Contains(".") && !format.StartsWith("timestamp") && !format.StartsWith("time "))
            {
                customTypes.Add(format);
                return "text";
            }

            return format switch
            {
                "uuid" => "uuid",
                "timestamp with time zone" => "timestamptz",
                "timestamp without time zone" => "timestamp",
                "date" => "date",
                "time with time zone" => "timetz",
                "time without time zone" => "time",
                "bigint" => "bigint",
                "integer" => "integer",
                "smallint" => "smallint",
                "numeric" => "numeric",
                "real" => "real",
                "double precision" => "double precision",
                "boolean" => "boolean",
                "jsonb" => "jsonb",
                "json" => "json",
                "bytea" => "bytea",
                "text[]" => "text[]",
                "uuid[]" => "uuid[]",
                _ => format.Contains(".") ? "text" : format
            };
        }

        return type switch
        {
            "integer" => "integer",
            "number" => "numeric",
            "boolean" => "boolean",
            "array" => "jsonb",
            "object" => "jsonb",
            _ => "text"
        };
    }
}

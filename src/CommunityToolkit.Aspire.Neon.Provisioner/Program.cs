using CommunityToolkit.Aspire.Neon.Api;
using System.Text.Json;

#if NEON_PROVISIONER_ENTRYPOINT

try
{
    var apiKey = Require(NeonProvisionerEnvironmentVariables.ApiKey);
    var mode = ReadMode();
    var createResources = mode == "provision";
    var useConnectionPooler = ReadBool(NeonProvisionerEnvironmentVariables.UseConnectionPooler);
    var outputFilePath = Require(NeonProvisionerEnvironmentVariables.OutputFilePath);
    DeleteFailureArtifact(outputFilePath);

    Console.WriteLine($"Neon provisioner starting. Mode={mode}, CreateResources={createResources}, UseConnectionPooler={useConnectionPooler}, Output={outputFilePath}");

    using var client = new NeonApiClient(apiKey);

    if (mode is "suspend" or "resume")
    {
        var commandProjectId = Require(NeonProvisionerEnvironmentVariables.ProjectId);
        var commandEndpointId = Require(NeonProvisionerEnvironmentVariables.EndpointId);

        if (mode == "suspend")
        {
            await client.SuspendEndpointAsync(commandProjectId, commandEndpointId, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"Neon endpoint suspended. Project={commandProjectId}, Endpoint={commandEndpointId}");
        }
        else
        {
            await client.StartEndpointAsync(commandProjectId, commandEndpointId, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"Neon endpoint resumed. Project={commandProjectId}, Endpoint={commandEndpointId}");
        }

        return 0;
    }

    var organizationId = await ResolveOrganizationIdAsync(client);
    Console.WriteLine($"Organization resolved. OrganizationId={organizationId ?? "<default>"}");

    var projectId = await ResolveProjectIdAsync(client, organizationId, createResources);
    Console.WriteLine($"Project resolved. ProjectId={projectId}");

    var branchId = await ResolveBranchIdAsync(client, projectId, createResources);
    Console.WriteLine($"Branch resolved. BranchId={branchId}, Ephemeral={ReadBool(NeonProvisionerEnvironmentVariables.UseEphemeralBranch)}");

    if (createResources && ReadBool(NeonProvisionerEnvironmentVariables.BranchRestoreEnabled))
    {
        Console.WriteLine("Applying branch restore options.");

        string? restoreSourceBranchId = Optional(NeonProvisionerEnvironmentVariables.BranchRestoreSourceBranchId);
        if (string.IsNullOrWhiteSpace(restoreSourceBranchId))
        {
            restoreSourceBranchId = await ResolveParentBranchIdAsync(client, projectId).ConfigureAwait(false);
        }

        await client.RestoreBranchAsync(projectId, branchId, new NeonApiBranchRestoreOptions
        {
            SourceBranchId = restoreSourceBranchId,
            SourceLsn = Optional(NeonProvisionerEnvironmentVariables.BranchRestoreSourceLsn),
            SourceTimestamp = ReadDateTimeOffset(NeonProvisionerEnvironmentVariables.BranchRestoreSourceTimestamp),
            PreserveUnderName = Optional(NeonProvisionerEnvironmentVariables.BranchRestorePreserveUnderName),
        }, CancellationToken.None).ConfigureAwait(false);
    }

    if (createResources && ReadBool(NeonProvisionerEnvironmentVariables.BranchSetAsDefault))
    {
        Console.WriteLine("Setting branch as default.");
        await client.SetDefaultBranchAsync(projectId, branchId, CancellationToken.None).ConfigureAwait(false);
    }

    var endpointId = await ResolveEndpointIdAsync(client, projectId, branchId, createResources);
    Console.WriteLine($"Endpoint resolved. EndpointId={endpointId}");

    var defaultDatabaseName = Read(NeonProvisionerEnvironmentVariables.DatabaseName, "neondb");
    var defaultRoleName = Read(NeonProvisionerEnvironmentVariables.RoleName, $"{defaultDatabaseName}_owner");

    List<NeonProvisionerDatabaseSpec> databaseSpecs =
    [
        new()
        {
            ResourceName = string.Empty,
            DatabaseName = defaultDatabaseName,
            RoleName = defaultRoleName,
        },
    ];

    databaseSpecs.AddRange(ParseDatabaseSpecs());

    List<NeonProvisionerDatabaseOutput> databaseOutputs = [];

    foreach (var database in databaseSpecs)
    {
        Console.WriteLine($"Ensuring role/database. Resource={database.ResourceName}, Database={database.DatabaseName}, Role={database.RoleName}");
        await EnsureRoleAsync(client, projectId, branchId, database.RoleName, createResources).ConfigureAwait(false);
        await EnsureDatabaseAsync(client, projectId, branchId, database.DatabaseName, database.RoleName, createResources).ConfigureAwait(false);

        var connectionUri = await client
            .GetConnectionUriAsync(
                projectId,
                branchId,
                endpointId,
                database.DatabaseName,
                database.RoleName,
                useConnectionPooler,
                CancellationToken.None)
            .ConfigureAwait(false);

        var connectionInfo = NeonConnectionInfo.Parse(connectionUri);
        databaseOutputs.Add(new NeonProvisionerDatabaseOutput
        {
            ResourceName = database.ResourceName,
            DatabaseName = database.DatabaseName,
            RoleName = database.RoleName,
            ConnectionUri = connectionUri,
            Host = connectionInfo.Host,
            Port = connectionInfo.Port,
            Password = connectionInfo.Password,
        });

        Console.WriteLine($"Connection URI resolved. Resource={database.ResourceName}, Database={database.DatabaseName}, Host={connectionInfo.Host}, Port={connectionInfo.Port}");
    }

    var defaultOutput = databaseOutputs.First(output =>
        string.Equals(output.DatabaseName, defaultDatabaseName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(output.RoleName, defaultRoleName, StringComparison.OrdinalIgnoreCase));

    var result = new NeonProvisionerOutput
    {
        ProjectId = projectId,
        BranchId = branchId,
        EndpointId = endpointId,
        DefaultDatabaseName = defaultDatabaseName,
        DefaultRoleName = defaultRoleName,
        DefaultConnectionUri = defaultOutput.ConnectionUri,
        Host = defaultOutput.Host,
        Port = defaultOutput.Port,
        Password = defaultOutput.Password,
        EndpointType = Read(NeonProvisionerEnvironmentVariables.EndpointType, "read_write"),
        Databases = databaseOutputs,
    };

    var outputDirectory = Path.GetDirectoryName(outputFilePath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    var outputJson = JsonSerializer.Serialize(result);
    await File.WriteAllTextAsync(outputFilePath, outputJson, CancellationToken.None).ConfigureAwait(false);

    foreach (var dbOutput in databaseOutputs)
    {
        var envFileName = string.IsNullOrEmpty(dbOutput.ResourceName)
            ? "default.env"
            : $"{dbOutput.ResourceName}.env";
        var envFilePath = Path.Combine(outputDirectory!, envFileName);
        var envContent =
            $"NEON_HOST={ShellEscape(dbOutput.Host ?? string.Empty)}\n" +
            $"NEON_PORT={ShellEscape(dbOutput.Port?.ToString() ?? "5432")}\n" +
            $"NEON_DATABASE={ShellEscape(dbOutput.DatabaseName)}\n" +
            $"NEON_USERNAME={ShellEscape(dbOutput.RoleName)}\n" +
            $"NEON_PASSWORD={ShellEscape(dbOutput.Password ?? string.Empty)}\n" +
            $"NEON_CONNECTION_URI={ShellEscape(dbOutput.ConnectionUri)}\n";
        await File.WriteAllTextAsync(envFilePath, envContent, CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"Env file written. Resource={dbOutput.ResourceName}, Path={envFilePath}");
    }

    Console.WriteLine($"Neon provisioner completed ({mode}). Project={projectId}, Branch={branchId}, Endpoint={endpointId}, Output={outputFilePath}");
    return 0;
}
catch (Exception ex)
{
    string? outputFilePath = Optional(NeonProvisionerEnvironmentVariables.OutputFilePath);
    TryWriteFailureArtifact(outputFilePath, ex);
    Console.Error.WriteLine($"Neon provisioner failed: {ex.Message}");
    return 1;
}

static void DeleteFailureArtifact(string outputFilePath)
{
    try
    {
        string failurePath = GetFailureArtifactPath(outputFilePath);
        if (File.Exists(failurePath))
        {
            File.Delete(failurePath);
        }
    }
    catch
    {
    }
}

static void TryWriteFailureArtifact(string? outputFilePath, Exception exception)
{
    if (string.IsNullOrWhiteSpace(outputFilePath))
    {
        return;
    }

    try
    {
        string failurePath = GetFailureArtifactPath(outputFilePath);
        string? directory = Path.GetDirectoryName(failurePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(failurePath, exception.ToString());
    }
    catch
    {
    }
}

static string GetFailureArtifactPath(string outputFilePath) => $"{outputFilePath}.error.log";

static string ShellEscape(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";

static string Require(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Required environment variable '{name}' was not provided.");
    }

    return value;
}

static string? Optional(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static string Read(string name, string fallback)
{
    return Optional(name) ?? fallback;
}

static bool ReadBool(string name)
{
    return bool.TryParse(Environment.GetEnvironmentVariable(name), out var result) && result;
}

static int? ReadInt(string name)
{
    return int.TryParse(Environment.GetEnvironmentVariable(name), out var result) ? result : null;
}

static bool? ReadNullableBool(string name)
{
    return bool.TryParse(Environment.GetEnvironmentVariable(name), out var result) ? result : null;
}

static DateTimeOffset? ReadDateTimeOffset(string name)
{
    return DateTimeOffset.TryParse(Environment.GetEnvironmentVariable(name), out var result) ? result : null;
}

static string ReadMode()
{
    var mode = Read(NeonProvisionerEnvironmentVariables.Mode, "attach").Trim().ToLowerInvariant();
    if (mode is not "attach" and not "provision" and not "suspend" and not "resume")
    {
        throw new InvalidOperationException($"Unsupported Neon mode '{mode}'. Allowed values are 'attach', 'provision', 'suspend', and 'resume'.");
    }

    return mode;
}

static async Task<string?> ResolveOrganizationIdAsync(NeonApiClient client)
{
    var organizationId = Optional(NeonProvisionerEnvironmentVariables.OrganizationId);
    if (!string.IsNullOrWhiteSpace(organizationId))
    {
        var organization = await client.GetOrganizationAsync(organizationId, CancellationToken.None).ConfigureAwait(false);
        if (organization is null)
        {
            throw new InvalidOperationException($"Neon organization '{organizationId}' was not found.");
        }

        return organization.Value.Id;
    }

    var organizationName = Optional(NeonProvisionerEnvironmentVariables.OrganizationName);
    if (string.IsNullOrWhiteSpace(organizationName))
    {
        return null;
    }

    var byName = await client.FindOrganizationByNameAsync(organizationName, CancellationToken.None).ConfigureAwait(false);
    if (byName is null)
    {
        throw new InvalidOperationException($"Neon organization '{organizationName}' was not found.");
    }

    return byName.Value.Id;
}

static async Task<string> ResolveProjectIdAsync(NeonApiClient client, string? organizationId, bool createResources)
{
    var configuredProjectId = Optional(NeonProvisionerEnvironmentVariables.ProjectId);
    if (!string.IsNullOrWhiteSpace(configuredProjectId))
    {
        return configuredProjectId;
    }

    var projectName = Optional(NeonProvisionerEnvironmentVariables.ProjectName);
    if (string.IsNullOrWhiteSpace(projectName))
    {
        throw new InvalidOperationException("Either NEON_PROJECT_ID or NEON_PROJECT_NAME must be provided.");
    }

    var existing = await client.FindProjectByNameAsync(projectName, organizationId, CancellationToken.None).ConfigureAwait(false);
    if (existing is not null)
    {
        Console.WriteLine($"Using existing project by name. Name={projectName}, ProjectId={existing.Value.Id}");
        return existing.Value.Id;
    }

    if (!createResources || !ReadBool(NeonProvisionerEnvironmentVariables.CreateProjectIfMissing))
    {
        throw new InvalidOperationException($"Neon project '{projectName}' was not found in attach mode.");
    }

    var branchName = Read(NeonProvisionerEnvironmentVariables.BranchName, "main");
    var databaseName = Read(NeonProvisionerEnvironmentVariables.DatabaseName, "neondb");
    var roleName = Read(NeonProvisionerEnvironmentVariables.RoleName, $"{databaseName}_owner");

    var created = await client.CreateProjectAsync(new NeonApiProjectCreateOptions
    {
        ProjectName = projectName,
        RegionId = Optional(NeonProvisionerEnvironmentVariables.RegionId),
        PostgresVersion = ReadInt(NeonProvisionerEnvironmentVariables.PostgresVersion),
        OrganizationId = organizationId,
        BranchName = branchName,
        DatabaseName = databaseName,
        RoleName = roleName,
    }, CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine($"Created project. Name={projectName}, ProjectId={created.Id}");

    return created.Id;
}

static async Task<string> ResolveBranchIdAsync(NeonApiClient client, string projectId, bool createResources)
{
    if (ReadBool(NeonProvisionerEnvironmentVariables.UseEphemeralBranch))
    {
        if (!createResources)
        {
            throw new InvalidOperationException("Ephemeral branch mode requires 'provision' mode.");
        }

        Console.WriteLine("Ephemeral branch mode enabled.");

        return await CreateEphemeralBranchAsync(client, projectId).ConfigureAwait(false);
    }

    var configuredBranchId = Optional(NeonProvisionerEnvironmentVariables.BranchId);
    if (!string.IsNullOrWhiteSpace(configuredBranchId))
    {
        return configuredBranchId;
    }

    var branchName = Optional(NeonProvisionerEnvironmentVariables.BranchName);
    if (!string.IsNullOrWhiteSpace(branchName))
    {
        var branch = await client.FindBranchByNameAsync(projectId, branchName, CancellationToken.None).ConfigureAwait(false);
        if (branch is not null)
        {
            Console.WriteLine($"Using existing branch by name. Name={branchName}, BranchId={branch.Value.Id}");
            return branch.Value.Id;
        }

        if (!createResources || !ReadBool(NeonProvisionerEnvironmentVariables.CreateBranchIfMissing))
        {
            throw new InvalidOperationException($"Neon branch '{branchName}' was not found in attach mode.");
        }

        var parentBranchId = await ResolveParentBranchIdAsync(client, projectId).ConfigureAwait(false);
        return await CreateBranchAsync(client, projectId, branchName, parentBranchId).ConfigureAwait(false);
    }

    var defaultTargetBranch = await client.GetDefaultBranchAsync(projectId, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Using default branch. BranchId={defaultTargetBranch.Id}, Name={defaultTargetBranch.Name}");
    return defaultTargetBranch.Id;
}

static async Task<string> CreateEphemeralBranchAsync(NeonApiClient client, string projectId)
{
    var prefix = Read(NeonProvisionerEnvironmentVariables.EphemeralBranchPrefix, "aspire-");
    Console.WriteLine($"Preparing ephemeral branch. Prefix={prefix}");

    await DeleteBranchesWithPrefixAsync(client, projectId, prefix).ConfigureAwait(false);

    var parentBranchId = await ResolveParentBranchIdAsync(client, projectId).ConfigureAwait(false);
    var branchName = $"{prefix}{Guid.NewGuid():N}";
    var expiresAt = DateTimeOffset.UtcNow.AddDays(1);
    Console.WriteLine($"Creating ephemeral branch. Name={branchName}, ExpiresAt={expiresAt:O}");

    var branchOptions = CreateBranchOptions(expiresAt);

    if (!ReadBool(NeonProvisionerEnvironmentVariables.BranchAnonymizationEnabled))
    {
        var created = await client.CreateBranchAsync(
            projectId,
            branchName,
            parentBranchId,
            branchOptions,
            CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"Created ephemeral branch. BranchId={created.Id}");

        return created.Id;
    }

    var anonymized = await client.CreateAnonymizedBranchAsync(
        projectId,
        branchName,
        parentBranchId,
        branchOptions,
        new NeonApiAnonymizationOptions
        {
            StartAnonymization = ReadBool(NeonProvisionerEnvironmentVariables.BranchAnonymizationStart),
            MaskingRules = ParseMaskingRules(),
        },
        CancellationToken.None).ConfigureAwait(false);

    Console.WriteLine($"Created anonymized ephemeral branch. BranchId={anonymized.Id}");

    return anonymized.Id;
}

static async Task DeleteBranchesWithPrefixAsync(NeonApiClient client, string projectId, string prefix)
{
    var branches = await client.GetBranchesAsync(projectId, CancellationToken.None).ConfigureAwait(false);
    foreach (var branch in branches)
    {
        if (!branch.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        Console.WriteLine($"Deleting prior ephemeral branch. BranchId={branch.Id}, Name={branch.Name}");
        await client.DeleteBranchAsync(projectId, branch.Id, CancellationToken.None).ConfigureAwait(false);
    }
}

static async Task<string> ResolveParentBranchIdAsync(NeonApiClient client, string projectId)
{
    var parentBranchId = Optional(NeonProvisionerEnvironmentVariables.ParentBranchId);
    if (!string.IsNullOrWhiteSpace(parentBranchId))
    {
        return parentBranchId;
    }

    var parentBranchName = Optional(NeonProvisionerEnvironmentVariables.ParentBranchName);
    if (!string.IsNullOrWhiteSpace(parentBranchName))
    {
        var parent = await client.FindBranchByNameAsync(projectId, parentBranchName, CancellationToken.None).ConfigureAwait(false);
        if (parent is null)
        {
            throw new InvalidOperationException($"Parent branch '{parentBranchName}' was not found.");
        }

        return parent.Value.Id;
    }

    var defaultBranch = await client.GetDefaultBranchAsync(projectId, CancellationToken.None).ConfigureAwait(false);
    return defaultBranch.Id;
}

static NeonApiBranchCreateOptions CreateBranchOptions(DateTimeOffset? expiresAt = null)
{
    return new NeonApiBranchCreateOptions
    {
        EndpointType = Read(NeonProvisionerEnvironmentVariables.EndpointType, "read_write"),
        InitSource = Optional(NeonProvisionerEnvironmentVariables.BranchInitSource) ?? "parent-data",
        ExpiresAt = expiresAt ?? ReadDateTimeOffset(NeonProvisionerEnvironmentVariables.BranchExpiresAt),
        ParentLsn = Optional(NeonProvisionerEnvironmentVariables.BranchParentLsn),
        ParentTimestamp = ReadDateTimeOffset(NeonProvisionerEnvironmentVariables.BranchParentTimestamp),
        Protected = ReadNullableBool(NeonProvisionerEnvironmentVariables.BranchProtected),
        Archived = ReadNullableBool(NeonProvisionerEnvironmentVariables.BranchArchived),
    };
}

static async Task<string> CreateBranchAsync(
    NeonApiClient client,
    string projectId,
    string branchName,
    string? parentBranchId)
{
    if (!ReadBool(NeonProvisionerEnvironmentVariables.BranchAnonymizationEnabled))
    {
        var created = await client.CreateBranchAsync(
            projectId,
            branchName,
            parentBranchId,
            CreateBranchOptions(),
            CancellationToken.None).ConfigureAwait(false);

        return created.Id;
    }

    var anonymized = await client.CreateAnonymizedBranchAsync(
        projectId,
        branchName,
        parentBranchId,
        CreateBranchOptions(),
        new NeonApiAnonymizationOptions
        {
            StartAnonymization = ReadBool(NeonProvisionerEnvironmentVariables.BranchAnonymizationStart),
            MaskingRules = ParseMaskingRules(),
        },
        CancellationToken.None).ConfigureAwait(false);

    return anonymized.Id;
}

static IReadOnlyList<NeonApiMaskingRule> ParseMaskingRules()
{
    var json = Optional(NeonProvisionerEnvironmentVariables.BranchMaskingRulesJson);
    if (string.IsNullOrWhiteSpace(json))
    {
        return [];
    }

    var rules = JsonSerializer.Deserialize<List<JsonElement>>(json);
    if (rules is null)
    {
        return [];
    }

    return rules.Select(rule => new NeonApiMaskingRule
    {
        DatabaseName = TryGetString(rule, "DatabaseName") ?? string.Empty,
        SchemaName = TryGetString(rule, "SchemaName") ?? "public",
        TableName = TryGetString(rule, "TableName") ?? string.Empty,
        ColumnName = TryGetString(rule, "ColumnName") ?? string.Empty,
        MaskingFunction = TryGetString(rule, "MaskingFunction"),
        MaskingValue = TryGetString(rule, "MaskingValue"),
    }).ToArray();
}

static IReadOnlyList<NeonProvisionerDatabaseSpec> ParseDatabaseSpecs()
{
    var json = Optional(NeonProvisionerEnvironmentVariables.DatabaseSpecsJson);
    if (string.IsNullOrWhiteSpace(json))
    {
        return [];
    }

    var specs = JsonSerializer.Deserialize<List<NeonProvisionerDatabaseSpec>>(json);
    return specs ?? [];
}

static string? TryGetString(JsonElement element, string propertyName)
{
    if (element.ValueKind != JsonValueKind.Object)
    {
        return null;
    }

    if (!element.TryGetProperty(propertyName, out var value))
    {
        return null;
    }

    return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

static async Task<string> ResolveEndpointIdAsync(NeonApiClient client, string projectId, string branchId, bool createResources)
{
    var configuredEndpointId = Optional(NeonProvisionerEnvironmentVariables.EndpointId);
    if (!string.IsNullOrWhiteSpace(configuredEndpointId))
    {
        return configuredEndpointId;
    }

    var endpointType = Read(NeonProvisionerEnvironmentVariables.EndpointType, "read_write");

    var endpoint = await client.GetEndpointByTypeAsync(projectId, branchId, endpointType, CancellationToken.None).ConfigureAwait(false);
    if (endpoint is not null)
    {
        Console.WriteLine($"Using existing endpoint. Type={endpointType}, EndpointId={endpoint.Value.Id}");
        return endpoint.Value.Id;
    }

    if (!createResources || !ReadBool(NeonProvisionerEnvironmentVariables.CreateEndpointIfMissing))
    {
        throw new InvalidOperationException($"No Neon endpoint of type '{endpointType}' was found for branch '{branchId}' in attach mode.");
    }

    var created = await client.CreateEndpointAsync(projectId, branchId, endpointType, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"Created endpoint. Type={endpointType}, EndpointId={created.Id}");
    return created.Id;
}

static async Task EnsureRoleAsync(NeonApiClient client, string projectId, string branchId, string roleName, bool createResources)
{
    var roleExists = await client.FindRoleAsync(projectId, branchId, roleName, CancellationToken.None).ConfigureAwait(false);
    if (!roleExists)
    {
        if (!createResources)
        {
            throw new InvalidOperationException($"Neon role '{roleName}' was not found on branch '{branchId}' in attach mode.");
        }

        await client.CreateRoleAsync(projectId, branchId, roleName, CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"Created role. Role={roleName}, BranchId={branchId}");
        return;
    }

    Console.WriteLine($"Role exists. Role={roleName}, BranchId={branchId}");
}

static async Task EnsureDatabaseAsync(
    NeonApiClient client,
    string projectId,
    string branchId,
    string databaseName,
    string roleName,
    bool createResources)
{
    var databaseExists = await client.FindDatabaseAsync(projectId, branchId, databaseName, CancellationToken.None).ConfigureAwait(false);
    if (!databaseExists)
    {
        if (!createResources)
        {
            throw new InvalidOperationException($"Neon database '{databaseName}' was not found on branch '{branchId}' in attach mode.");
        }

        await client.CreateDatabaseAsync(projectId, branchId, databaseName, roleName, CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"Created database. Database={databaseName}, Owner={roleName}, BranchId={branchId}");
        return;
    }

    Console.WriteLine($"Database exists. Database={databaseName}, BranchId={branchId}");
}

#endif
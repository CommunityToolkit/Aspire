// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Neon.Api;

internal sealed class NeonApiClient : IDisposable
{
    private static readonly Uri BaseUri = new("https://console.neon.tech/api/v2/");
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient;

    public NeonApiClient(string apiKey)
    {
        _httpClient = new HttpClient { BaseAddress = BaseUri, Timeout = DefaultTimeout };

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CommunityToolkit.Aspire.Neon/1.0");
    }

    public async Task<NeonProjectInfo?> FindProjectByNameAsync(
        string projectName,
        string? organizationId,
        CancellationToken cancellationToken
    )
    {
        var url = $"projects?search={Uri.EscapeDataString(projectName)}&limit=100";
        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            url = $"{url}&org_id={Uri.EscapeDataString(organizationId)}";
        }

        using var document = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("projects", out var projects))
        {
            return null;
        }

        foreach (var project in projects.EnumerateArray())
        {
            if (project.TryGetProperty("name", out var nameElement)
                && string.Equals(
                    nameElement.GetString(),
                    projectName,
                    StringComparison.OrdinalIgnoreCase))
            {
                var id = project.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return new NeonProjectInfo(id, nameElement.GetString() ?? projectName);
                }
            }
        }

        return null;
    }

    public async Task<NeonOrganizationInfo?> GetOrganizationAsync(
        string organizationId,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"organizations/{organizationId}");
        using var response = await _httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Neon API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). {errorBody}");
        }

        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
        return ParseOrganization(document.RootElement, organizationId);
    }

    public async Task<NeonOrganizationInfo?> FindOrganizationByNameAsync(
        string organizationName,
        CancellationToken cancellationToken
    )
    {
        using var document = await GetJsonAsync("users/me/organizations", cancellationToken)
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("organizations", out var organizations))
        {
            return null;
        }

        foreach (var organization in organizations.EnumerateArray())
        {
            if (organization.TryGetProperty("name", out var nameElement)
                && string.Equals(
                    nameElement.GetString(),
                    organizationName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return ParseOrganization(organization, organizationName);
            }
        }

        return null;
    }

    public async Task<NeonProjectInfo> CreateProjectAsync(
        NeonApiProjectCreateOptions options,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(options.ProjectName))
        {
            throw new InvalidOperationException(
                "A project name must be provided to create a Neon project."
            );
        }

        var projectPayload = new Dictionary<string, object?>
        {
            ["name"] = options.ProjectName,
            ["branch"] = new Dictionary<string, object?>
            {
                ["name"] = options.BranchName,
                ["database_name"] = options.DatabaseName,
                ["role_name"] = options.RoleName,
            },
        };

        if (!string.IsNullOrWhiteSpace(options.RegionId))
        {
            projectPayload["region_id"] = options.RegionId;
        }

        if (options.PostgresVersion.HasValue)
        {
            projectPayload["pg_version"] = options.PostgresVersion.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.OrganizationId))
        {
            projectPayload["org_id"] = options.OrganizationId;
        }

        var payload = new Dictionary<string, object?> { ["project"] = projectPayload };

        using var response = await SendAsync(
                HttpMethod.Post,
                "projects",
                payload,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("project", out var projectElement))
        {
            throw new InvalidOperationException(
                "Neon project creation response did not include a project payload."
            );
        }

        var id = projectElement.GetProperty("id").GetString();
        var name = projectElement.GetProperty("name").GetString();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                "Neon project creation response did not include a project id."
            );
        }

        return new NeonProjectInfo(id, name ?? options.ProjectName);
    }

    private static NeonOrganizationInfo ParseOrganization(
        JsonElement element,
        string fallbackName
    )
    {
        if (element.TryGetProperty("organization", out var organizationElement))
        {
            element = organizationElement;
        }

        var id = element.TryGetProperty("id", out var idElement)
            ? idElement.GetString()
            : null;
        var name = element.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                "Neon organization response did not include an organization id."
            );
        }

        return new NeonOrganizationInfo(id, name ?? fallbackName);
    }

    public async Task<NeonBranchInfo?> FindBranchByNameAsync(
        string projectId,
        string branchName,
        CancellationToken cancellationToken
    )
    {
        var url = $"projects/{projectId}/branches?search={Uri.EscapeDataString(branchName)}&limit=100";

        using var document = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("branches", out var branches))
        {
            return null;
        }

        foreach (var branch in branches.EnumerateArray())
        {
            if (branch.TryGetProperty("name", out var nameElement) &&
                string.Equals(nameElement.GetString(), branchName, StringComparison.OrdinalIgnoreCase))
            {
                var id = branch.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return new NeonBranchInfo(id, nameElement.GetString() ?? branchName);
                }
            }
        }

        return null;
    }

    public async Task<NeonBranchInfo> GetDefaultBranchAsync(
        string projectId,
        CancellationToken cancellationToken
    )
    {
        using var document = await GetJsonAsync(
                $"projects/{projectId}/branches?limit=100",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("branches", out var branches))
        {
            throw new InvalidOperationException(
                "Neon branch list response did not include branches."
            );
        }

        foreach (var branch in branches.EnumerateArray())
        {
            if (branch.TryGetProperty("default", out var defaultElement) && defaultElement.GetBoolean())
            {
                var id = branch.GetProperty("id").GetString();
                var name = branch.GetProperty("name").GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return new NeonBranchInfo(id, name ?? "default");
                }
            }
        }

        throw new InvalidOperationException("Neon project does not have a default branch.");
    }

    public async Task<IReadOnlyList<NeonBranchInfo>> GetBranchesAsync(
        string projectId,
        CancellationToken cancellationToken
    )
    {
        using var document = await GetJsonAsync(
                $"projects/{projectId}/branches?limit=100",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("branches", out var branches))
        {
            return [];
        }

        List<NeonBranchInfo> results = [];
        foreach (var branch in branches.EnumerateArray())
        {
            var id = branch.GetProperty("id").GetString();
            var name = branch.GetProperty("name").GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                results.Add(new NeonBranchInfo(id, name ?? string.Empty));
            }
        }

        return results;
    }

    public async Task<NeonBranchInfo> CreateBranchAsync(
        string projectId,
        string branchName,
        string? parentBranchId,
        NeonApiBranchCreateOptions branchOptions,
        CancellationToken cancellationToken
    )
    {
        var branchPayload = new Dictionary<string, object?>
        {
            ["name"] = branchName,
            ["parent_id"] = parentBranchId,
            ["init_source"] = branchOptions.InitSource,
        };

        if (branchOptions.Protected.HasValue)
        {
            branchPayload["protected"] = branchOptions.Protected.Value;
        }

        if (branchOptions.ExpiresAt.HasValue)
        {
            branchPayload["expires_at"] = branchOptions.ExpiresAt.Value;
        }

        if (!string.IsNullOrWhiteSpace(branchOptions.ParentLsn))
        {
            branchPayload["parent_lsn"] = branchOptions.ParentLsn;
        }

        if (branchOptions.ParentTimestamp.HasValue)
        {
            branchPayload["parent_timestamp"] = branchOptions.ParentTimestamp.Value;
        }

        if (branchOptions.Archived.HasValue)
        {
            branchPayload["archived"] = branchOptions.Archived.Value;
        }

        var payload = new Dictionary<string, object?>
        {
            ["branch"] = branchPayload,
            ["endpoints"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = branchOptions.EndpointType
                }
            }
        };

        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/branches",
                payload,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("branch", out var branchElement))
        {
            throw new InvalidOperationException(
                "Neon create branch response did not include branch information."
            );
        }

        var id = branchElement.GetProperty("id").GetString();
        var name = branchElement.GetProperty("name").GetString();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                "Neon create branch response did not include branch id."
            );
        }

        return new NeonBranchInfo(id, name ?? branchName);
    }

    public async Task<NeonBranchInfo> CreateAnonymizedBranchAsync(
        string projectId,
        string branchName,
        string? parentBranchId,
        NeonApiBranchCreateOptions branchOptions,
        NeonApiAnonymizationOptions anonymizationOptions,
        CancellationToken cancellationToken
    )
    {
        var branchPayload = new Dictionary<string, object?>
        {
            ["name"] = branchName,
            ["parent_id"] = parentBranchId,
            ["init_source"] = branchOptions.InitSource,
        };

        if (branchOptions.Protected.HasValue)
        {
            branchPayload["protected"] = branchOptions.Protected.Value;
        }

        if (branchOptions.ExpiresAt.HasValue)
        {
            branchPayload["expires_at"] = branchOptions.ExpiresAt.Value;
        }

        if (!string.IsNullOrWhiteSpace(branchOptions.ParentLsn))
        {
            branchPayload["parent_lsn"] = branchOptions.ParentLsn;
        }

        if (branchOptions.ParentTimestamp.HasValue)
        {
            branchPayload["parent_timestamp"] = branchOptions.ParentTimestamp.Value;
        }

        var maskingRules = anonymizationOptions.MaskingRules.Select(rule =>
        {
            Dictionary<string, object?> maskingRule = new()
            {
                ["database_name"] = rule.DatabaseName,
                ["schema_name"] = rule.SchemaName,
                ["table_name"] = rule.TableName,
                ["column_name"] = rule.ColumnName,
                ["masking_function"] = rule.MaskingFunction,
            };

            if (!string.IsNullOrWhiteSpace(rule.MaskingValue))
            {
                maskingRule["masking_value"] = rule.MaskingValue;
            }

            return maskingRule;
        }).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["branch_create"] = new Dictionary<string, object?>
            {
                ["branch"] = branchPayload,
                ["endpoints"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = branchOptions.EndpointType
                    }
                }
            },
            ["masking_rules"] = maskingRules,
            ["start_anonymization"] = anonymizationOptions.StartAnonymization,
        };

        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/branch_anonymized",
                payload,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("branch", out var branchElement))
        {
            throw new InvalidOperationException(
                "Neon create anonymized branch response did not include branch information."
            );
        }

        var id = branchElement.GetProperty("id").GetString();
        var name = branchElement.GetProperty("name").GetString();

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                "Neon create anonymized branch response did not include branch id."
            );
        }

        return new NeonBranchInfo(id, name ?? branchName);
    }

    public async Task RestoreBranchAsync(
        string projectId,
        string branchId,
        NeonApiBranchRestoreOptions restoreOptions,
        CancellationToken cancellationToken
    )
    {
        var payload = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(restoreOptions.SourceBranchId))
        {
            payload["source_branch_id"] = restoreOptions.SourceBranchId;
        }

        if (!string.IsNullOrWhiteSpace(restoreOptions.SourceLsn))
        {
            payload["source_lsn"] = restoreOptions.SourceLsn;
        }

        if (restoreOptions.SourceTimestamp.HasValue)
        {
            payload["source_timestamp"] = restoreOptions.SourceTimestamp.Value;
        }

        if (!string.IsNullOrWhiteSpace(restoreOptions.PreserveUnderName))
        {
            payload["preserve_under_name"] = restoreOptions.PreserveUnderName;
        }

        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/branches/{branchId}/restore",
                payload,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
    }

    public async Task SetDefaultBranchAsync(
        string projectId,
        string branchId,
        CancellationToken cancellationToken
    )
    {
        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/branches/{branchId}/set_as_default",
                null,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
    }

    public async Task<bool> FindDatabaseAsync(
        string projectId,
        string branchId,
        string databaseName,
        CancellationToken cancellationToken
    )
    {
        using var document = await GetJsonAsync(
                $"projects/{projectId}/branches/{branchId}/databases",
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("databases", out var databases))
        {
            return false;
        }

        foreach (var db in databases.EnumerateArray())
        {
            if (db.TryGetProperty("name", out var nameElement) &&
                string.Equals(nameElement.GetString(), databaseName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task CreateDatabaseAsync(
        string projectId,
        string branchId,
        string databaseName,
        string ownerName,
        CancellationToken cancellationToken
    )
    {
        var payload = new Dictionary<string, object?>
        {
            ["database"] = new Dictionary<string, object?>
            {
                ["name"] = databaseName,
                ["owner_name"] = ownerName,
            }
        };

        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/branches/{branchId}/databases",
                payload,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
    }

    public async Task<bool> FindRoleAsync(
        string projectId,
        string branchId,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        using var document = await GetJsonAsync(
                $"projects/{projectId}/branches/{branchId}/roles",
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("roles", out var roles))
        {
            return false;
        }

        foreach (var role in roles.EnumerateArray())
        {
            if (role.TryGetProperty("name", out var nameElement) &&
                string.Equals(nameElement.GetString(), roleName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task CreateRoleAsync(
        string projectId,
        string branchId,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        var payload = new Dictionary<string, object?>
        {
            ["role"] = new Dictionary<string, object?>
            {
                ["name"] = roleName,
            }
        };

        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/branches/{branchId}/roles",
                payload,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
    }

    public async Task DeleteBranchAsync(
        string projectId,
        string branchId,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"projects/{projectId}/branches/{branchId}");
        using var response = await _httpClient.SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        throw new InvalidOperationException(
            $"Neon API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). {errorBody}");
    }

    public async Task<NeonEndpointInfo?> GetEndpointByTypeAsync(
        string projectId,
        string branchId,
        string endpointType,
        CancellationToken cancellationToken
    )
    {
        using var document = await GetJsonAsync(
                $"projects/{projectId}/branches/{branchId}/endpoints",
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("endpoints", out var endpoints))
        {
            return null;
        }

        foreach (var endpoint in endpoints.EnumerateArray())
        {
            if (endpoint.TryGetProperty("type", out var typeElement) &&
                string.Equals(typeElement.GetString(), endpointType, StringComparison.OrdinalIgnoreCase))
            {
                var id = endpoint.GetProperty("id").GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return NeonEndpointInfo.Parse(endpoint);
                }
            }
        }

        return null;
    }

    public async Task<NeonEndpointInfo> CreateEndpointAsync(
        string projectId,
        string branchId,
        string endpointType,
        CancellationToken cancellationToken
    )
    {
        var payload = new Dictionary<string, object?>
        {
            ["endpoint"] = new Dictionary<string, object?>
            {
                ["branch_id"] = branchId,
                ["type"] = endpointType
            }
        };

        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/endpoints",
                payload,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("endpoint", out var endpointElement))
        {
            throw new InvalidOperationException(
                "Neon create endpoint response did not include endpoint information."
            );
        }

        return NeonEndpointInfo.Parse(endpointElement);
    }

    public async Task SuspendEndpointAsync(
        string projectId,
        string endpointId,
        CancellationToken cancellationToken
    )
    {
        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/endpoints/{endpointId}/suspend",
                null,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
    }

    public async Task StartEndpointAsync(
        string projectId,
        string endpointId,
        CancellationToken cancellationToken
    )
    {
        using var response = await SendAsync(
                HttpMethod.Post,
                $"projects/{projectId}/endpoints/{endpointId}/start",
                null,
            cancellationToken,
            allowRetry: true
            )
            .ConfigureAwait(false);
    }

    public async Task<string> GetConnectionUriAsync(
        string projectId,
        string branchId,
        string? endpointId,
        string databaseName,
        string roleName,
        bool pooled,
        CancellationToken cancellationToken
    )
    {
        var query = new List<string>
        {
            $"branch_id={Uri.EscapeDataString(branchId)}",
            $"database_name={Uri.EscapeDataString(databaseName)}",
            $"role_name={Uri.EscapeDataString(roleName)}"
        };

        if (!string.IsNullOrWhiteSpace(endpointId))
        {
            query.Add($"endpoint_id={Uri.EscapeDataString(endpointId)}");
        }

        if (pooled)
        {
            query.Add("pooled=true");
        }

        var url = $"projects/{projectId}/connection_uri?{string.Join("&", query)}";

        using var response = await SendAsync(
                HttpMethod.Get,
                url,
                null,
                cancellationToken,
                allowRetry: true
            )
            .ConfigureAwait(false);
        using var document = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("uri", out var uriElement))
        {
            throw new InvalidOperationException(
                "Neon connection URI response did not include a uri."
            );
        }

        var uri = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new InvalidOperationException(
                "Neon connection URI response contained an empty uri."
            );
        }

        return uri;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken)
            .ConfigureAwait(false);
        return await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? payload,
        CancellationToken cancellationToken,
        bool allowRetry = false
    )
    {
        var attempt = 0;
        while (true)
        {
            using var request = new HttpRequestMessage(method, url);

            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (allowRetry && ShouldRetry(response.StatusCode) && attempt < 4)
            {
                attempt++;
                await Task.Delay(TimeSpan.FromSeconds(1 + attempt), cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Neon API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). {errorBody}"
            );
        }
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) => statusCode is HttpStatusCode.Locked
        or HttpStatusCode.Conflict
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.TooManyRequests;

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose() => _httpClient.Dispose();
}

internal readonly record struct NeonProjectInfo(string Id, string Name);

internal readonly record struct NeonOrganizationInfo(string Id, string Name);

internal readonly record struct NeonBranchInfo(string Id, string Name);

internal readonly record struct NeonEndpointInfo(
    string Id,
    string? Host,
    string? Type = null,
    string? RegionId = null,
    string? CurrentState = null,
    decimal? AutoscalingLimitMinCu = null,
    decimal? AutoscalingLimitMaxCu = null,
    int? SuspendTimeoutSeconds = null,
    bool? PoolerEnabled = null,
    string? ProxyHost = null,
    string? BranchId = null,
    string? CreatedAt = null
)
{
    internal static NeonEndpointInfo Parse(JsonElement endpoint)
    {
        var id = endpoint.GetProperty("id").GetString()
            ?? throw new InvalidOperationException(
                "Neon endpoint response did not include an endpoint id.");

        return new NeonEndpointInfo(
            Id: id,
            Host: endpoint.TryGetProperty("host", out var h) ? h.GetString() : null,
            Type: endpoint.TryGetProperty("type", out var t) ? t.GetString() : null,
            RegionId: endpoint.TryGetProperty("region_id", out var r) ? r.GetString() : null,
            CurrentState: endpoint.TryGetProperty("current_state", out var cs) ? cs.GetString() : null,
            AutoscalingLimitMinCu: endpoint.TryGetProperty("autoscaling_limit_min_cu", out var minCu) ? minCu.GetDecimal() : null,
            AutoscalingLimitMaxCu: endpoint.TryGetProperty("autoscaling_limit_max_cu", out var maxCu) ? maxCu.GetDecimal() : null,
            SuspendTimeoutSeconds: endpoint.TryGetProperty("suspend_timeout_seconds", out var sts) ? sts.GetInt32() : null,
            PoolerEnabled: endpoint.TryGetProperty("pooler_enabled", out var pe) ? pe.GetBoolean() : null,
            ProxyHost: endpoint.TryGetProperty("proxy_host", out var ph) ? ph.GetString() : null,
            BranchId: endpoint.TryGetProperty("branch_id", out var bi) ? bi.GetString() : null,
            CreatedAt: endpoint.TryGetProperty("created_at", out var ca) ? ca.GetString() : null
        );
    }
}

internal readonly record struct NeonConnectionInfo(
    string Host,
    int Port,
    string Database,
    string Role,
    string Password
)
{
    public static NeonConnectionInfo Parse(string connectionUri)
    {
        var uri = new Uri(connectionUri);
        var database = uri.AbsolutePath.TrimStart('/');
        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        var role = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = uri.Host;
        var port = uri.IsDefaultPort ? 5432 : uri.Port;

        return new NeonConnectionInfo(host, port, database, role, password);
    }
 }

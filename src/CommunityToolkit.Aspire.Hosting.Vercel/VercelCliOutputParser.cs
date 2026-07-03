using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelCliOutputParser
{
    public static string GetDeploymentUrl(string standardOutput)
        => GetDeploymentResult(standardOutput).DeploymentUrl;

    public static VercelDeploymentResult GetDeploymentResult(string standardOutput)
    {
        // `vercel deploy` output has changed between CLI versions and flags. Prefer structured
        // JSON when present, then fall back to the last plain HTTP(S) URL printed by the CLI.
        if (TryGetJsonDeploymentResult(standardOutput) is { } jsonDeploymentResult)
        {
            return jsonDeploymentResult;
        }

        // Older CLI versions printed the deployment URL as plain text. Keep the fallback so
        // we fail only when no usable URL exists, not because formatting changed slightly.
        string[] lines = standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        string? deploymentUrl = lines.LastOrDefault(IsHttpUrl);
        if (deploymentUrl is null)
        {
            throw new DistributedApplicationException($"Vercel deploy output did not contain an HTTP or HTTPS deployment URL. Output: {GetTrimmedOutput(standardOutput)}");
        }

        return new(DeploymentId: null, deploymentUrl);
    }

    public static VercelDeploymentInspection GetDeploymentInspection(string standardOutput)
    {
        try
        {
            // Parse the Vercel inspect JSON shapes observed across CLI versions:
            //   { "readyState": "READY" }
            //   { "state": "READY" }
            //   { "deployment": { "readyState": "READY" } }
            //   { "deployment": { "state": "READY" } }
            var output = JsonSerializer.Deserialize<VercelInspectOutput>(standardOutput);
            string? readyState = output?.ReadyState
                ?? output?.State
                ?? output?.Deployment?.ReadyState
                ?? output?.Deployment?.State;

            return new(readyState);
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException("Failed to parse JSON output from 'vercel inspect'.", ex);
        }
    }

    public static bool TryGetCliVersion(string output, [NotNullWhen(true)] out Version? version)
    {
        // The CLI can print banners/warnings around the version. Extract the first semantic
        // x.y.z token instead of requiring a line to be exactly the version string.
        var match = Regex.Match(output, @"(?<!\d)(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?!\d)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success)
        {
            version = null;
            return false;
        }

        version = new(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture));
        return true;
    }

    public static bool ProjectListContainsProject(string standardOutput, string projectName)
    {
        try
        {
            foreach (var project in DeserializeArrayOrNamedArray<VercelListedProject>(standardOutput, "projects"))
            {
                if (string.Equals(project.Name, projectName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException("Failed to parse JSON output from 'vercel project ls'.", ex);
        }
    }

    public static bool EnvironmentVariableListContainsName(string standardOutput, string name)
    {
        try
        {
            foreach (var environmentVariable in DeserializeArrayOrNamedArray<VercelListedEnvironmentVariable>(standardOutput, "envs"))
            {
                if (string.Equals(environmentVariable.Key, name, StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(environmentVariable.GitBranch))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException("Failed to parse JSON output from 'vercel env ls'.", ex);
        }
    }

    public static string GetTrimmedOutput(string output)
        => string.IsNullOrWhiteSpace(output) ? "<empty>" : output.Trim();

    private static VercelDeploymentResult? TryGetJsonDeploymentResult(string standardOutput)
    {
        if (!standardOutput.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var output = JsonSerializer.Deserialize<VercelDeployOutput>(standardOutput);
            if (output is not null && TryGetDeploymentResult(output, out var deploymentResult))
            {
                return deploymentResult;
            }
        }
        catch (JsonException)
        {
            // Older Vercel CLI output is plain text; fall back to line-based URL extraction.
        }

        return null;
    }

    private static bool TryGetDeploymentResult(VercelDeployOutput output, [NotNullWhen(true)] out VercelDeploymentResult? deploymentResult)
    {
        // Parse the Vercel deploy JSON shapes observed from different CLI versions:
        //   { "deployment": { "url": "https://...", "id": "..." } }
        //   { "url": "https://...", "id": "..." }
        // Callers fall back to line-based extraction when deploy output is plain text.
        if (TryGetHttpUrl(output.Deployment?.Url, out var nestedDeploymentUrl))
        {
            deploymentResult = new(output.Deployment?.Id, nestedDeploymentUrl);
            return true;
        }

        if (TryGetHttpUrl(output.Url, out var rootDeploymentUrl))
        {
            deploymentResult = new(output.Id, rootDeploymentUrl);
            return true;
        }

        deploymentResult = null;
        return false;
    }

    private static bool TryGetHttpUrl(string? url, [NotNullWhen(true)] out string? deploymentUrl)
    {
        deploymentUrl = url;
        return IsHttpUrl(deploymentUrl);
    }

    private static bool IsHttpUrl([NotNullWhen(true)] string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";

    private static T[] DeserializeArrayOrNamedArray<T>(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.Deserialize<T[]>() ?? [];
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            return array.Deserialize<T[]>() ?? [];
        }

        throw new JsonException($"Expected JSON array or object property '{propertyName}'.");
    }

    private sealed class VercelDeployOutput
    {
        [JsonPropertyName("deployment")]
        public VercelDeployDeployment? Deployment { get; init; }

        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class VercelDeployDeployment
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class VercelInspectOutput
    {
        [JsonPropertyName("readyState")]
        public string? ReadyState { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }

        [JsonPropertyName("deployment")]
        public VercelInspectDeployment? Deployment { get; init; }
    }

    private sealed class VercelInspectDeployment
    {
        [JsonPropertyName("readyState")]
        public string? ReadyState { get; init; }

        [JsonPropertyName("state")]
        public string? State { get; init; }
    }

    private sealed class VercelListedProject
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed class VercelListedEnvironmentVariable
    {
        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("gitBranch")]
        public string? GitBranch { get; init; }
    }
}

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES002
#pragma warning disable ASPIREPIPELINES003
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable ASPIRECOMPUTE003
#pragma warning disable ASPIREPROBES001
#pragma warning disable CTASPIREVERCEL001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static partial class VercelDeploymentStep
{
    private static async Task<string> PrepareProjectEnvironmentDirectoryAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        string projectLinkDirectory = Path.Combine(outputService.GetTempDirectory(entry.Resource), ".vercel-project");
        if (Directory.Exists(projectLinkDirectory))
        {
            Directory.Delete(projectLinkDirectory, recursive: true);
        }

        Directory.CreateDirectory(projectLinkDirectory);

        // `vercel env add` is project-scoped but intentionally does not accept --project.
        // See https://vercel.com/docs/cli/env and https://vercel.com/docs/cli/link.
        // Link a scratch directory instead of the source root so secret configuration can use
        // the CLI's native project lookup without writing .vercel metadata into user code.
        string[] linkArguments = BuildLinkProjectArguments(options, projectLinkDirectory, GetVercelProjectOption(entry));
        var result = await runner.RunAsync(VercelCliFileName, linkArguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"prepare temporary Vercel project link for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }

        return projectLinkDirectory;
    }

    private static async Task<VercelPulledProject> PullProjectSettingsAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry,
        string projectLinkDirectory)
    {
        // `vercel pull` is the documented way to materialize project settings and environment
        // files under `.vercel/`; VCR authentication depends on the pulled VERCEL_OIDC_TOKEN.
        // See https://vercel.com/docs/cli/pull and https://vercel.com/docs/container-registry.
        string targetEnvironment = GetVercelProjectEnvironmentName(options);
        string[] arguments = BuildPullProjectSettingsArguments(options, projectLinkDirectory, targetEnvironment);
        var result = await runner.RunAsync(VercelCliFileName, arguments, projectLinkDirectory, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"pull Vercel project settings for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }

        string vercelDirectory = Path.Combine(projectLinkDirectory, VercelDirectoryName);
        string projectJsonPath = Path.Combine(vercelDirectory, VercelProjectFileName);
        string environmentPath = Path.Combine(vercelDirectory, $".env.{targetEnvironment}.local");

        if (!File.Exists(projectJsonPath))
        {
            throw new DistributedApplicationException($"Vercel pull did not write expected project settings file '{projectJsonPath}' for resource '{entry.Resource.Name}'.");
        }

        if (!File.Exists(environmentPath))
        {
            throw new DistributedApplicationException($"Vercel pull did not write expected environment file '{environmentPath}' for resource '{entry.Resource.Name}'.");
        }

        var environmentVariables = ParseDotEnvFile(await File.ReadAllLinesAsync(environmentPath, context.CancellationToken).ConfigureAwait(false));
        if (!environmentVariables.TryGetValue(VercelOidcTokenEnvironmentVariable, out string? oidcToken)
            || string.IsNullOrWhiteSpace(oidcToken))
        {
            throw new DistributedApplicationException($"Vercel pull did not provide {VercelOidcTokenEnvironmentVariable}, which is required to authenticate local Docker builds to VCR.");
        }

        string projectJsonContent = await File.ReadAllTextAsync(projectJsonPath, context.CancellationToken).ConfigureAwait(false);
        var project = ReadVercelProjectSettings(projectJsonPath, projectJsonContent);

        // `vercel pull` materializes project secrets next to the scratch link so local
        // builders can read them. This integration only needs the short-lived OIDC token
        // and project metadata; delete the env files before creating deploy artifacts.
        DeleteIfExists(environmentPath);
        DeleteIfExists(Path.Combine(projectLinkDirectory, ".env.local"));

        return new(project.ProjectName, project.ProjectId, project.OrgId, projectJsonContent, oidcToken);
    }

    private static async Task LoginToVcrAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        string oidcToken,
        VercelOidcClaims claims)
        => await LoginToVcrAsync(runner, oidcToken, claims, context.CancellationToken).ConfigureAwait(false);

    internal static async Task LoginToVcrAsync(
        IVercelCliRunner runner,
        string oidcToken,
        VercelOidcClaims claims,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claims.OwnerId))
        {
            throw new DistributedApplicationException("The Vercel OIDC token did not include the owner_id claim required to authenticate to VCR.");
        }

        // VCR supports Docker-compatible tooling at vcr.vercel.com. This login uses the
        // Vercel-issued OIDC token pulled for the linked project.
        // See https://vercel.com/docs/container-registry.
        string[] arguments = BuildDockerLoginArguments(claims.OwnerId);
        var result = await runner.RunAsync(DockerCliFileName, arguments, workingDirectory: null, cancellationToken, standardInput: oidcToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException("authenticate Docker to VCR", DockerCliFileName, result);
        }
    }

    internal static async Task WriteBuildOutputAsync(
        VercelDeploymentEntry entry,
        VercelPulledProject project,
        string imageReference,
        CancellationToken cancellationToken)
    {
        // Vercel Build Output API v3 expects the file-system contract documented at
        // https://vercel.com/docs/build-output-api and
        // https://vercel.com/docs/build-output-api/configuration:
        //   .vercel/project.json                          copied project identity from `vercel pull`
        //   .vercel/output/config.json                    routes and API version
        //   .vercel/output/functions/index.func/.vc-config.json
        //       { "runtime": "container", "handler": "<vcr image>@sha256:..." }
        // There is intentionally no user source copy here; Aspire's build/push pipeline has
        // already built the image, and Vercel deploy uploads only metadata that points at it.
        string vercelDirectory = Path.Combine(entry.DeployDirectory, VercelDirectoryName);
        string outputDirectory = Path.Combine(vercelDirectory, VercelOutputDirectoryName);
        string functionDirectory = Path.Combine(outputDirectory, "functions", "index.func");
        Directory.CreateDirectory(functionDirectory);

        await File.WriteAllTextAsync(Path.Combine(vercelDirectory, VercelProjectFileName), project.ProjectJsonContent, cancellationToken).ConfigureAwait(false);

        var outputConfig = new JsonObject
        {
            ["version"] = VercelBuildOutputApiVersion,
            ["routes"] = new JsonArray
            {
                new JsonObject
                {
                    ["handle"] = "filesystem"
                },
                new JsonObject
                {
                    ["src"] = "/(.*)",
                    ["dest"] = "/index"
                }
            }
        };

        var functionConfig = new JsonObject
        {
            ["handler"] = imageReference,
            ["runtime"] = "container",
            ["environment"] = new JsonObject()
        };

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "config.json"), outputConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(functionDirectory, ".vc-config.json"), functionConfig.ToJsonString(JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    private static VercelPulledProjectSettings ReadVercelProjectSettings(string projectJsonPath, string projectJsonContent)
    {
        try
        {
            // `vercel pull` writes `.vercel/project.json`. Only project identity fields are
            // needed: they select the linked provider project and are safe to persist in state.
            using var document = JsonDocument.Parse(projectJsonContent);
            var root = document.RootElement;

            string projectName = root.TryGetProperty("projectName", out var projectNameElement) && projectNameElement.ValueKind == JsonValueKind.String
                ? projectNameElement.GetString() ?? string.Empty
                : string.Empty;
            string? projectId = root.TryGetProperty("projectId", out var projectIdElement) && projectIdElement.ValueKind == JsonValueKind.String
                ? projectIdElement.GetString()
                : null;
            string? orgId = root.TryGetProperty("orgId", out var orgIdElement) && orgIdElement.ValueKind == JsonValueKind.String
                ? orgIdElement.GetString()
                : null;

            return new(projectName, projectId, orgId);
        }
        catch (JsonException ex)
        {
            throw new DistributedApplicationException($"Vercel project settings file '{projectJsonPath}' is invalid JSON.", ex);
        }
    }

    internal static string GetDockerImageDigest(string output)
    {
        string trimmed = output.Trim();
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            try
            {
                // `docker buildx imagetools inspect --format '{{json .Digest}}'` style output
                // is a JSON string. Older experiments used this shape before Vercel required
                // selecting the concrete linux/amd64 manifest from the OCI index.
                string? digest = JsonSerializer.Deserialize<string>(trimmed);
                if (IsSha256Digest(digest))
                {
                    return digest!;
                }
            }
            catch (JsonException ex)
            {
                throw new DistributedApplicationException("Docker returned invalid JSON while resolving the pushed VCR image digest.", ex);
            }
        }

        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                // Current path uses `--format '{{json .Manifest}}'`. Docker may return an OCI
                // image index with a `manifests[]` array, or a single manifest object. Vercel's
                // Container Images docs describe VCR-backed OCI images; live smoke tests rejected
                // index digests, so prefer the linux/amd64 child.
                // See https://vercel.com/docs/functions/container-images.
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;

                if (root.TryGetProperty("manifests", out var manifests) && manifests.ValueKind == JsonValueKind.Array)
                {
                    foreach (var manifest in manifests.EnumerateArray())
                    {
                        if (manifest.TryGetProperty("platform", out var platform)
                            && platform.TryGetProperty("os", out var osElement)
                            && platform.TryGetProperty("architecture", out var architectureElement)
                            && string.Equals(osElement.GetString(), "linux", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(architectureElement.GetString(), "amd64", StringComparison.OrdinalIgnoreCase)
                            && TryGetJsonString(manifest, "digest", out var platformDigest)
                            && IsSha256Digest(platformDigest))
                        {
                            return platformDigest!;
                        }
                    }

                    throw new DistributedApplicationException("Docker did not return a linux/amd64 manifest digest for the pushed VCR image. Vercel requires linux/amd64 container images.");
                }

                if (TryGetJsonString(root, "digest", out var digest) && IsSha256Digest(digest))
                {
                    return digest!;
                }
            }
            catch (JsonException ex)
            {
                throw new DistributedApplicationException("Docker returned invalid JSON while resolving the pushed VCR image digest.", ex);
            }
        }

        var match = Regex.Match(trimmed, @"sha256:[a-fA-F0-9]{64}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (match.Success)
        {
            return match.Value;
        }

        throw new DistributedApplicationException($"Docker did not return a valid sha256 image digest. Output: {GetTrimmedOutput(output)}");
    }

    private static bool TryGetJsonString(JsonElement element, string propertyName, [NotNullWhen(true)] out string? value)
    {
        value = element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

        return !string.IsNullOrWhiteSpace(value);
    }

    internal static VercelOidcClaims DecodeUnvalidatedOidcClaims(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new DistributedApplicationException("The Vercel OIDC token is not a valid compact JWT.");
        }

        try
        {
            // This is an unvalidated decode of the Vercel-issued token from `vercel pull`.
            // Docker/Vercel validate the token when it is used; here we only need routing
            // metadata such as owner_id/project to construct the VCR login and repository.
            byte[] payloadBytes = Convert.FromBase64String(PadBase64Url(parts[1]));
            using var document = JsonDocument.Parse(payloadBytes);
            var root = document.RootElement;

            return new(
                GetStringClaim(root, "owner_id"),
                GetStringClaim(root, "owner"),
                GetStringClaim(root, "project"),
                GetStringClaim(root, "project_id"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new DistributedApplicationException("The Vercel OIDC token payload could not be decoded.", ex);
        }
    }

    private static string? GetStringClaim(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static string PadBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        return padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
    }

    internal static Dictionary<string, string> ParseDotEnvFile(IEnumerable<string> lines)
    {
        // Vercel writes dotenv files such as `.vercel/.env.production.local` during pull.
        // We only need the VERCEL_OIDC_TOKEN line. This intentionally supports the subset the
        // CLI emits: comments/blank lines, KEY=value, single/double quoted values, and common
        // backslash escapes. It is not a general dotenv evaluator with interpolation.
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            values[key] = UnquoteDotEnvValue(value);
        }

        return values;
    }

    private static string UnquoteDotEnvValue(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return value.Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static bool IsSha256Digest([NotNullWhen(true)] string? value)
        => value is not null && Regex.IsMatch(value, "^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task EnsureManagedProjectAsync(
        PipelineStepContext context,
        IVercelCliRunner runner,
        VercelEnvironmentOptionsAnnotation options,
        VercelDeploymentEntry entry)
    {
        // `vercel project add` is idempotent for the current login/scope in the CLI versions
        // this integration supports: it creates the project or validates that it already
        // exists and is accessible. Failure here means deploy should not proceed to image push.
        string projectName = GetVercelProjectName(entry);
        string[] arguments = BuildAddProjectArguments(options, projectName);
        var result = await runner.RunAsync(VercelCliFileName, arguments, workingDirectory: null, context.CancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw CreateCliException($"create or validate Vercel project '{projectName}' for resource '{entry.Resource.Name}'", VercelCliFileName, result);
        }
    }

    private static string GetVercelProjectEnvironmentName(VercelEnvironmentOptionsAnnotation options)
    {
        if (options.Production)
        {
            return "production";
        }

        return string.IsNullOrWhiteSpace(options.Target) ? "preview" : options.Target;
    }

    private static string GetVercelProjectEnvironmentName(VercelDeploymentState state)
    {
        if (state.Production)
        {
            return "production";
        }

        return string.IsNullOrWhiteSpace(state.Target) ? "preview" : state.Target;
    }
}

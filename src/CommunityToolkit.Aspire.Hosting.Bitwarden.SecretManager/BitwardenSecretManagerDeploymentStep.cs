#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

/// <summary>
/// Patches Bitwarden-resolved values into environment files written by <c>prepare-{env}</c>.
/// </summary>
/// <remarks>
/// Workaround for PrepareAsync (Aspire.Hosting.Docker) not calling GetValueAsync on custom
/// IValueProvider sources — it only resolves ParameterResource and ContainerImageReference,
/// leaving Bitwarden-derived env vars blank. Remove once fixed upstream.
/// </remarks>
internal static class BitwardenSecretManagerDeploymentStep
{
    internal static async Task PatchEnvFilesAsync(PipelineStepContext context, BitwardenSecretManagerResource bitwarden)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        var hostEnvironment = context.Services.GetService<IHostEnvironment>();
        var environmentName = hostEnvironment?.EnvironmentName ?? "Production";

        var computeEnvironments = context.Model.Resources.OfType<IComputeEnvironmentResource>().ToList();
        if (computeEnvironments.Count == 0)
        {
            return;
        }

        var patches = BuildBitwardenPatches(bitwarden);
        if (patches.Count == 0)
        {
            return;
        }

        foreach (var computeEnv in computeEnvironments)
        {
            string outputDir = computeEnvironments.Count > 1
                ? outputService.GetOutputDirectory(computeEnv)
                : outputService.GetOutputDirectory();

            string envFilePath = Path.Combine(outputDir, $".env.{environmentName}");

            if (!File.Exists(envFilePath))
            {
                continue;
            }

            await PatchEnvFileAsync(envFilePath, patches, context.Logger).ConfigureAwait(false);
        }
    }

    private static Dictionary<string, string> BuildBitwardenPatches(BitwardenSecretManagerResource bitwarden)
    {
        var patches = new Dictionary<string, string>(StringComparer.Ordinal);

        if (bitwarden.ProjectId is Guid projectId)
        {
            patches[ToEnvKey($"{{{bitwarden.Name}.projectId}}")] = projectId.ToString("D");
        }

        foreach (var secret in bitwarden.ManagedSecrets)
        {
            string? secretValue = bitwarden.ResolveSecretValue(secret);
            if (secretValue is not null)
            {
                patches[ToEnvKey($"{{{bitwarden.Name}.secrets.{secret.RemoteName}}}")] = secretValue;
            }

            if (secret.SecretId is Guid secretId)
            {
                patches[ToEnvKey($"{{{bitwarden.Name}.secrets.{secret.RemoteName}.id}}")] = secretId.ToString("D");
            }
        }

        foreach (var secretRef in bitwarden.DeclaredSecretReferences)
        {
            if (secretRef.IsManaged)
            {
                continue; // already handled in ManagedSecrets loop above
            }

            string? secretValue = bitwarden.ResolveSecretValue(secretRef);
            if (secretValue is not null)
            {
                patches[ToEnvKey($"{{{bitwarden.Name}.secrets.{secretRef.RemoteName}}}")] = secretValue;
            }

            if (secretRef.ResolvedSecretId is Guid secretId)
            {
                patches[ToEnvKey($"{{{bitwarden.Name}.secrets.{secretRef.RemoteName}.id}}")] = secretId.ToString("D");
            }
        }

        return patches;
    }

    private static async Task PatchEnvFileAsync(string envFilePath, Dictionary<string, string> patches, ILogger logger)
    {
        var lines = await File.ReadAllLinesAsync(envFilePath).ConfigureAwait(false);
        bool modified = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            int eqIdx = line.IndexOf('=');
            if (eqIdx <= 0)
            {
                continue;
            }

            string key = line[..eqIdx].Trim();
            if (!patches.TryGetValue(key, out string? newValue))
            {
                continue;
            }

            string currentValue = eqIdx < line.Length - 1 ? line[(eqIdx + 1)..] : string.Empty;
            if (!string.IsNullOrEmpty(currentValue))
            {
                continue; // preserve existing values
            }

            lines[i] = $"{key}={newValue}";
            modified = true;
            logger.LogInformation("Populated '{Key}' with Bitwarden-resolved value.", key);
        }

        if (modified)
        {
            await File.WriteAllLinesAsync(envFilePath, lines).ConfigureAwait(false);
        }
    }

    private static string ToEnvKey(string valueExpression) =>
        valueExpression
            .Replace("{", "").Replace("}", "")
            .Replace(".", "_").Replace("-", "_")
            .ToUpperInvariant();
}

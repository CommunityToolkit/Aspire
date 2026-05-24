using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

internal sealed class BitwardenStateStore(IServiceProvider services)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<BitwardenStateFileContext> LoadAsync(BitwardenSecretManagerResource resource, string resolvedProjectName, string authPath, CancellationToken cancellationToken)
    {
        string statePath = ResolveStatePath(resource, resolvedProjectName);

        if (!File.Exists(statePath))
        {
            return new(statePath, authPath, new BitwardenState());
        }

        try
        {
            await using FileStream stream = File.OpenRead(statePath);
            BitwardenState? state = await JsonSerializer.DeserializeAsync<BitwardenState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            state ??= new BitwardenState();
            state.Normalize();
            return new(statePath, authPath, state);
        }
        catch (Exception ex)
        {
            throw new DistributedApplicationException($"Failed to load Bitwarden state file from '{statePath}'.", ex);
        }
    }

    public async Task SaveAsync(string path, BitwardenState state, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(state);

        state.Normalize();

        string directory = Path.GetDirectoryName(path) ?? throw new DistributedApplicationException($"Unable to determine the Bitwarden state file directory for path '{path}'.");

        try
        {
            Directory.CreateDirectory(directory);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new DistributedApplicationException($"Failed to save Bitwarden state file to '{path}'.", ex);
        }
    }

    private string ResolveStatePath(BitwardenSecretManagerResource resource, string resolvedProjectName)
    {
        IAspireStore aspireStore = services.GetRequiredService<IAspireStore>();

        if (resource.StateFile is { Length: > 0 } stateFile)
        {
            return Path.IsPathRooted(stateFile)
                ? stateFile
                : Path.GetFullPath(Path.Combine(aspireStore.BasePath, stateFile));
        }

        string directory = Path.Combine(aspireStore.BasePath, "bitwarden");
        Directory.CreateDirectory(directory);

        string safeResourceName = string.Concat(resource.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
        string identityHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(resource.GetConfiguredProjectIdentityKey(resolvedProjectName))))[..12].ToLowerInvariant();
        string defaultPath = Path.Combine(directory, $"{safeResourceName}.{identityHash}.state.json");

        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        string[] existingPaths = Directory.GetFiles(directory, $"{safeResourceName}.*.state.json", SearchOption.TopDirectoryOnly);
        return existingPaths.Length == 1 ? existingPaths[0] : defaultPath;
    }
}

internal sealed record BitwardenStateFileContext(string Path, string AuthPath, BitwardenState State);

internal sealed class BitwardenState
{
    public Guid? ProjectId { get; set; }

    public Dictionary<string, Guid> ManagedSecretIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Guid> NameBindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        ManagedSecretIds = new Dictionary<string, Guid>(ManagedSecretIds, StringComparer.OrdinalIgnoreCase);
        NameBindings = new Dictionary<string, Guid>(NameBindings, StringComparer.OrdinalIgnoreCase);
    }
}

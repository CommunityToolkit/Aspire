using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

internal sealed class BitwardenStore(IServiceProvider services)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<BitwardenCacheContext> LoadAsync(BitwardenSecretManagerResource resource, string resolvedProjectName, string authCachePath, CancellationToken cancellationToken)
    {
        string cachePath = ResolveCachePath(resource, resolvedProjectName);

        if (!File.Exists(cachePath))
        {
            return new(cachePath, authCachePath, new BitwardenCache());
        }

        try
        {
            await using FileStream stream = File.OpenRead(cachePath);
            BitwardenCache? cache = await JsonSerializer.DeserializeAsync<BitwardenCache>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            cache ??= new BitwardenCache();
            cache.Normalize();
            return new(cachePath, authCachePath, cache);
        }
        catch (Exception ex)
        {
            throw new DistributedApplicationException($"Failed to load Bitwarden AppHost cache file from '{cachePath}'.", ex);
        }
    }

    public async Task SaveAsync(string path, BitwardenCache cache, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(cache);

        cache.Normalize();

        string directory = Path.GetDirectoryName(path) ?? throw new DistributedApplicationException($"Unable to determine the Bitwarden AppHost cache file directory for path '{path}'.");

        try
        {
            Directory.CreateDirectory(directory);
            await using FileStream stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, cache, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new DistributedApplicationException($"Failed to save Bitwarden AppHost cache file to '{path}'.", ex);
        }
    }

    private string ResolveCachePath(BitwardenSecretManagerResource resource, string resolvedProjectName)
    {
        if (resource.CacheFile is { Length: > 0 } cacheFile)
        {
            if (Path.IsPathRooted(cacheFile))
            {
                return cacheFile;
            }

            IAspireStore aspireStore = services.GetRequiredService<IAspireStore>();
            return Path.GetFullPath(Path.Combine(aspireStore.BasePath, cacheFile));
        }

        IAspireStore store = services.GetRequiredService<IAspireStore>();
        string directory = Path.Combine(store.BasePath, "bitwarden");
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

internal sealed record BitwardenCacheContext(string CachePath, string AuthCachePath, BitwardenCache Cache);

internal sealed class BitwardenCache
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

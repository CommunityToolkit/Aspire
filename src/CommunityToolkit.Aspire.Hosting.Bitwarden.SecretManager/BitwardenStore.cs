using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

internal static class BitwardenStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<BitwardenCacheContext> LoadAsync(BitwardenSecretManagerResource resource, string authCachePath, CancellationToken cancellationToken)
    {
        string cachePath = resource.CacheFile!;
        string? directory = Path.GetDirectoryName(cachePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

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

    public static async Task SaveAsync(string path, BitwardenCache cache, CancellationToken cancellationToken)
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

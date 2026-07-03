using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Vercel;

internal static class VercelDockerImageDigestParser
{
    public static string GetDigest(string output)
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
                var manifestOutput = JsonSerializer.Deserialize<DockerManifestOutput>(trimmed);
                if (manifestOutput?.Manifests is { Length: > 0 } manifests)
                {
                    foreach (var manifest in manifests)
                    {
                        if (string.Equals(manifest.Platform?.Os, "linux", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(manifest.Platform?.Architecture, "amd64", StringComparison.OrdinalIgnoreCase)
                            && IsSha256Digest(manifest.Digest))
                        {
                            return manifest.Digest!;
                        }
                    }

                    throw new DistributedApplicationException("Docker did not return a linux/amd64 manifest digest for the pushed VCR image. Vercel requires linux/amd64 container images.");
                }

                if (IsSha256Digest(manifestOutput?.Digest))
                {
                    return manifestOutput!.Digest!;
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

        throw new DistributedApplicationException($"Docker did not return a valid sha256 image digest. Output: {VercelCliOutputParser.GetTrimmedOutput(output)}");
    }

    private static bool IsSha256Digest([NotNullWhen(true)] string? value)
        => value is not null && Regex.IsMatch(value, "^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private sealed class DockerManifestOutput
    {
        [JsonPropertyName("manifests")]
        public DockerManifest[]? Manifests { get; init; }

        [JsonPropertyName("digest")]
        public string? Digest { get; init; }
    }

    private sealed class DockerManifest
    {
        [JsonPropertyName("digest")]
        public string? Digest { get; init; }

        [JsonPropertyName("platform")]
        public DockerManifestPlatform? Platform { get; init; }
    }

    private sealed class DockerManifestPlatform
    {
        [JsonPropertyName("os")]
        public string? Os { get; init; }

        [JsonPropertyName("architecture")]
        public string? Architecture { get; init; }
    }
}

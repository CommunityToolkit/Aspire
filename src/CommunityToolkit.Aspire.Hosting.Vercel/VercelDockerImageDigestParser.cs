using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
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
                            && VercelJson.TryGetString(manifest, "digest", out var platformDigest)
                            && IsSha256Digest(platformDigest))
                        {
                            return platformDigest!;
                        }
                    }

                    throw new DistributedApplicationException("Docker did not return a linux/amd64 manifest digest for the pushed VCR image. Vercel requires linux/amd64 container images.");
                }

                if (VercelJson.TryGetString(root, "digest", out var digest) && IsSha256Digest(digest))
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

        throw new DistributedApplicationException($"Docker did not return a valid sha256 image digest. Output: {VercelCliOutputParser.GetTrimmedOutput(output)}");
    }

    private static bool IsSha256Digest([NotNullWhen(true)] string? value)
        => value is not null && Regex.IsMatch(value, "^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}

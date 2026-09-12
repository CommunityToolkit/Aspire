// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;

internal static class GlitchTipArtifactUploader
{
    internal static async Task UploadAsync(Uri instance, string token, string organization, string project,
        IReadOnlyList<GlitchTipArtifact> artifacts, string workingDirectory, CancellationToken cancellationToken)
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        await UploadAsync(http, instance, token, organization, project, artifacts, workingDirectory,
            TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
    }

    internal static async Task UploadAsync(HttpClient http, Uri instance, string token, string organization, string project,
        IReadOnlyList<GlitchTipArtifact> artifacts, string workingDirectory, TimeSpan pollInterval, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (artifacts.Count == 0)
        {
            return;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var session = new UploadSession(http, new Uri(instance.AbsoluteUri.TrimEnd('/') + "/"), token, pollInterval, deadline.Token);
        var org = Uri.EscapeDataString(organization);
        var projectPath = $"api/0/projects/{org}/{Uri.EscapeDataString(project)}/";
        try
        {
            foreach (var artifact in artifacts)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(artifact.Release);
                var path = Path.GetFullPath(artifact.Path, workingDirectory);
                var files = File.Exists(path) ? [path] : Directory.Exists(path)
                    ? Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).ToArray()
                    : [];
                if (files.Length == 0)
                {
                    if (artifact.Optional)
                    {
                        continue;
                    }

                    throw new DistributedApplicationException("A required GlitchTip artifact registration is missing or empty. Build the registered output before startup or deployment, or explicitly mark an expected absence optional.");
                }

                // Validate the whole native registration before making any remote changes.
                var symbols = artifact.Kind == GlitchTipArtifactKind.DebugSymbols
                    ? files.Where(IsNativeDebugFile).Order(StringComparer.Ordinal).ToArray()
                    : [];

                // Idempotent create associates the effective service release with this project.
                await session.JsonAsync(HttpMethod.Post, projectPath + "releases/", new { version = artifact.Release }).ConfigureAwait(false);
                if (artifact.Kind == GlitchTipArtifactKind.SourceMaps)
                {
                    await using var bundle = await GlitchTipSourceBundle.CreateAsync(path, organization, project, artifact.Release, deadline.Token).ConfigureAwait(false);
                    var listPath = projectPath + $"releases/{Uri.EscapeDataString(artifact.Release)}/files/";
                    if (await session.ContainsAllAsync(listPath, bundle.Expected).ConfigureAwait(false))
                    {
                        continue;
                    }

                    var (Checksum, Chunks) = await session.UploadChunksAsync(bundle.Stream, org).ConfigureAwait(false);
                    var result = await session.JsonAsync(HttpMethod.Post, $"api/0/organizations/{org}/artifactbundle/assemble/",
                        new { checksum = Checksum, chunks = Chunks, projects = new[] { project }, version = artifact.Release }).ConfigureAwait(false);
                    EnsureAssemblyAccepted(result);
                    // 6.2.6 reports 'created' before asynchronous parsing and does not expose
                    // worker failures through the assemble endpoint. Verify persisted artifacts.
                    await session.WaitForArtifactsAsync(listPath, bundle.Expected).ConfigureAwait(false);
                }
                else
                {

                    if (symbols.Length == 0)
                    {
                        if (artifact.Optional)
                        {
                            continue;
                        }

                        throw new DistributedApplicationException("The required GlitchTip debug-symbol registration contains no ELF, Mach-O/dSYM, Windows PDB, or portable PDB files.");
                    }

                    foreach (var file in symbols)
                    {
                        var expected = new GlitchTipExpectedArtifact(Path.GetFileName(file), await GlitchTipSourceBundle.ChecksumAsync(file, deadline.Token).ConfigureAwait(false));
                        if (await session.ContainsAllAsync(projectPath + "files/dsyms/", [expected]).ConfigureAwait(false))
                        {
                            continue;
                        }

                        await using var stream = File.OpenRead(file);
                        var (Checksum, Chunks) = await session.UploadChunksAsync(stream, org).ConfigureAwait(false);
                        var result = await session.JsonAsync(HttpMethod.Post, projectPath + "files/difs/assemble/",
                            new Dictionary<string, object> { [Checksum] = new { name = Path.GetFileName(file), chunks = Chunks } }).ConfigureAwait(false);
                        if (!result.TryGetProperty(Checksum, out var state))
                        {
                            throw new DistributedApplicationException("GlitchTip returned no assembly status for the requested debug file.");
                        }

                        EnsureAssemblyAccepted(state);
                        await session.WaitForArtifactsAsync(projectPath + "files/dsyms/", [expected]).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DistributedApplicationException("GlitchTip artifact upload or assembly verification timed out. Verify the instance's worker and file storage, then retry startup or deployment.");
        }
    }

    internal static void EnsureAssemblyAccepted(JsonElement response)
    {
        var state = response.TryGetProperty("state", out var value) ? value.GetString() : null;
        if (state is not ("ok" or "created" or "assembling") ||
            response.TryGetProperty("missingChunks", out var missing) && missing.GetArrayLength() != 0)
        {
            throw new DistributedApplicationException("GlitchTip rejected artifact assembly. Verify upload permissions and storage; artifact processing has not completed.");
        }
    }

    internal static bool IsNativeDebugFile(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[32];
        var length = file.ReadAtLeast(header, 4, throwOnEndOfStream: false);
        if (length < 4)
        {
            return false;
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (magic is 0xbebafeca or 0xcafebabe or 0xbfbafeca or 0xcafebabf)
        {
            // 6.2.6 stores only the first object from a native archive under (project, file).
            // A matching file checksum therefore cannot prove all architectures were processed.
            throw new DistributedApplicationException("GlitchTip 6.2.6 cannot verify every architecture in a universal (fat) Mach-O archive. Extract and register a separate thin debug file for each architecture.");
        }

        return header[..4].SequenceEqual("\u007fELF"u8) || header[..4].SequenceEqual("BSJB"u8) ||
            length >= 24 && header[..24].SequenceEqual("Microsoft C/C++ MSF 7.00\r\n"u8) ||
            magic is 0xfeedface or 0xfeedfacf or 0xcefaedfe or 0xcffaedfe;
    }

    private sealed class UploadSession(HttpClient http, Uri instance, string token, TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        internal async Task<JsonElement> JsonAsync(HttpMethod method, string path, object? payload = null)
        {
            using var response = await SendAsync(method, new Uri(instance, path), payload is null ? null : () => JsonContent.Create(payload)).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            return document.RootElement.Clone();
        }

        internal async Task<(string Checksum, string[] Chunks)> UploadChunksAsync(Stream stream, string org)
        {
            var settings = await JsonAsync(HttpMethod.Get, $"api/0/organizations/{org}/chunk-upload/").ConfigureAwait(false);
            if (settings.GetProperty("hashAlgorithm").GetString() != "sha1" || !settings.GetProperty("compression").EnumerateArray().Any(item => item.GetString() == "gzip"))
            {
                throw new DistributedApplicationException("This GlitchTip server does not expose the supported SHA-1/gzip chunk upload protocol.");
            }

            var target = new Uri(instance, settings.GetProperty("url").GetString()!);
            EnsureSameOrigin(target);
            var chunkSize = Math.Min(settings.GetProperty("chunkSize").GetInt32(), 8 * 1024 * 1024);
            if (chunkSize <= 0 || stream.Length > settings.GetProperty("maxFileSize").GetInt64())
            {
                throw new DistributedApplicationException("The registered GlitchTip artifact exceeds the server's upload limits.");
            }

            var chunks = new List<string>();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
            var buffer = new byte[chunkSize];
            while (true)
            {
                var length = await stream.ReadAtLeastAsync(buffer, chunkSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (length == 0)
                {
                    break;
                }

                hash.AppendData(buffer, 0, length);
                var checksum = Convert.ToHexStringLower(SHA1.HashData(buffer.AsSpan(0, length)));
                using var compressed = new MemoryStream();
                await using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                {
                    await gzip.WriteAsync(buffer.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
                }

                var bytes = compressed.ToArray();
                using var response = await SendAsync(HttpMethod.Post, target, () =>
                {
                    var form = new MultipartFormDataContent
                    {
                        { new ByteArrayContent(bytes), "file_gzip", checksum }
                    };
                    return form;
                }).ConfigureAwait(false);
                chunks.Add(checksum);
            }

            return (Convert.ToHexStringLower(hash.GetHashAndReset()), chunks.ToArray());
        }

        internal async Task WaitForArtifactsAsync(string path, IReadOnlyList<GlitchTipExpectedArtifact> expected)
        {
            while (!await ContainsAllAsync(path, expected).ConfigureAwait(false))
            {
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        internal async Task<bool> ContainsAllAsync(string path, IReadOnlyList<GlitchTipExpectedArtifact> expected)
        {
            var remaining = expected.ToList();
            Uri? next = new(instance, path);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (next is not null)
            {
                if (!visited.Add(next.AbsoluteUri))
                {
                    throw new DistributedApplicationException("GlitchTip returned a repeated artifact pagination link.");
                }

                using var response = await SendAsync(HttpMethod.Get, next, null).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
                var root = document.RootElement;
                var rows = root.ValueKind == JsonValueKind.Array ? root
                    : root.TryGetProperty("results", out var results) ? results : root.GetProperty("items");
                foreach (var row in rows.EnumerateArray())
                {
                    remaining.RemoveAll(item => Matches(row, item));
                }

                if (remaining.Count == 0)
                {
                    return true;
                }

                next = null;
                if (response.Headers.TryGetValues("Link", out var links))
                {
                    foreach (var link in links.SelectMany(value => value.Split(',')))
                    {
                        if (!link.Contains("rel=\"next\"", StringComparison.Ordinal) || link.Contains("results=\"false\"", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var start = link.IndexOf('<');
                        var end = link.IndexOf('>');
                        if (start >= 0 && end > start)
                        {
                            next = new Uri(instance, link[(start + 1)..end]);
                        }
                    }
                }
            }

            return false;
        }

        private static bool Matches(JsonElement row, GlitchTipExpectedArtifact artifact)
        {
            if (!row.TryGetProperty("sha1", out var sha1) || sha1.GetString() != artifact.Checksum)
            {
                return false;
            }

            if (artifact.DebugId is null)
            {
                return row.TryGetProperty("debugId", out var id) && !string.IsNullOrWhiteSpace(id.GetString());
            }

            return row.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object &&
                headers.TryGetProperty("debug-id", out var debugId) && debugId.GetString() == artifact.DebugId &&
                headers.TryGetProperty("x-aspire-sourcemap-sha1", out var mapChecksum) && mapChecksum.GetString() == artifact.SourceMapChecksum;
        }

        private void EnsureSameOrigin(Uri target)
        {
            if (target.Scheme != instance.Scheme || target.Host != instance.Host || target.Port != instance.Port || !string.IsNullOrEmpty(target.UserInfo))
            {
                throw new DistributedApplicationException("GlitchTip returned an upload or pagination URL outside the configured instance origin; management credentials were not forwarded.");
            }
        }

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri target, Func<HttpContent>? createContent)
        {
            EnsureSameOrigin(target);
            for (var attempt = 0; ; attempt++)
            {
                using var request = new HttpRequestMessage(method, target);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = createContent?.Invoke();
                HttpResponseMessage response;
                try
                {
                    response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }
                catch (HttpRequestException)
                {
                    throw new DistributedApplicationException("GlitchTip artifact API could not be reached after bounded retries.");
                }

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                var status = response.StatusCode;
                response.Dispose();
                if (attempt < 2 && (status == HttpStatusCode.TooManyRequests || (int)status >= 500))
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt + 1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Provider response bodies may echo credentials or file contents. Never log them.
                throw new DistributedApplicationException($"GlitchTip artifact API returned HTTP {(int)status}. Verify project/release permissions, instance availability, and artifact configuration.");
            }
        }
    }
}
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class DeploymentArtifactTests
{
    private static readonly string[] GzipCompression = ["gzip"];
    [Fact]
    public async Task NativeUploadUsesVerifiedChunksAndWaitsForParsedDebugId()
    {
        using var files = new ArtifactDirectory();
        var path = files.Write("app.pdb", "BSJBportable-pdb-test-fixture");
        var checksum = await GlitchTipSourceBundle.ChecksumAsync(path, default);
        var chunks = new List<byte[]>();
        var assembled = false;
        var listingCalls = 0;
        using var handler = new Handler(async request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            var uri = request.RequestUri!.AbsolutePath;
            if (uri.EndsWith("releases/", StringComparison.Ordinal))
            {
                Assert.Equal("v1", (await BodyAsync(request)).GetProperty("version").GetString());
                return Json(new { });
            }
            if (uri.EndsWith("files/dsyms/", StringComparison.Ordinal))
            {
                listingCalls++;
                return Json(assembled ? new[] { new { sha1 = checksum, debugId = "ad0c0274-68bd-4ef6-801a-77d67f9894ed" } } : []);
            }
            if (uri.EndsWith("chunk-upload/", StringComparison.Ordinal))
            {
                if (request.Method == HttpMethod.Get)
                {
                    return ChunkSettings(8);
                }
                var content = Assert.IsType<MultipartFormDataContent>(request.Content);
                var chunk = Assert.Single(content);
                using var compressed = await chunk.ReadAsStreamAsync();
                using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                using var raw = new MemoryStream();
                await gzip.CopyToAsync(raw);
                var bytes = raw.ToArray();
                Assert.Equal(Convert.ToHexStringLower(SHA1.HashData(bytes)), chunk.Headers.ContentDisposition!.FileName!.Trim('"'));
                Assert.Equal("file_gzip", chunk.Headers.ContentDisposition.Name!.Trim('"'));
                chunks.Add(bytes);
                return Json(new { });
            }
            var body = await BodyAsync(request);
            Assert.Equal(checksum, Assert.Single(body.EnumerateObject()).Name);
            var requestedChunks = body.GetProperty(checksum).GetProperty("chunks").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Equal(chunks.Select(chunk => Convert.ToHexStringLower(SHA1.HashData(chunk))), requestedChunks);
            assembled = true;
            return Json(new Dictionary<string, object> { [checksum] = new { state = "created", missingChunks = Array.Empty<string>() } });
        });
        using var http = new HttpClient(handler);
        await UploadAsync(http, files.Path, [new(path, "v1", false, GlitchTipArtifactKind.DebugSymbols)]);
        Assert.Equal(await File.ReadAllBytesAsync(path), chunks.SelectMany(chunk => chunk).ToArray());
        Assert.Equal(2, listingCalls);
        var initialChunks = chunks.Count;
        await UploadAsync(http, files.Path, [new(path, "v1", false, GlitchTipArtifactKind.DebugSymbols)]);
        Assert.Equal(initialChunks, chunks.Count);
    }

    [Fact]
    public async Task AcceptedAssemblyWithoutPersistedSymbolsDoesNotSucceed()
    {
        using var files = new ArtifactDirectory();
        var path = files.Write("bad.pdb", "BSJBnot-parseable");
        var checksum = await GlitchTipSourceBundle.ChecksumAsync(path, default);
        using var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("files/dsyms/", StringComparison.Ordinal))
            {
                return Json(new[] { new { sha1 = checksum, debugId = (string?)null } });
            }
            if (request.RequestUri.AbsolutePath.EndsWith("chunk-upload/", StringComparison.Ordinal))
            {
                return request.Method == HttpMethod.Get ? ChunkSettings() : Json(new { });
            }
            if (request.RequestUri.AbsolutePath.EndsWith("assemble/", StringComparison.Ordinal))
            {
                return Json(new Dictionary<string, object> { [checksum] = new { state = "created", missingChunks = Array.Empty<string>() } });
            }
            await BodyAsync(request);
            return Json(new { });
        });
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() => UploadAsync(http, files.Path,
            [new(path, "v1", false, GlitchTipArtifactKind.DebugSymbols)], TimeSpan.FromMilliseconds(100)));
        Assert.Contains("timed out", exception.Message);
    }

    [Fact]
    public async Task SourceBundlePreservesPreparedIdsAndVerifiesBothContents()
    {
        using var files = new ArtifactDirectory();
        const string id = "ad0c0274-68bd-4ef6-801a-77d67f9894ed";
        var source = files.Write("app.js", $"throw new Error('sample');\n//# debugId={id}\n//# sourceMappingURL=app.js.map\n");
        var map = files.Write("app.js.map", $$"""{"version":3,"debug_id":"{{id}}","sources":["source.ts"],"mappings":"AAAA"}""");
        await using var bundle = await GlitchTipSourceBundle.CreateAsync(files.Path, "org", "project", "v2", default);
        var expected = Assert.Single(bundle.Expected);
        Assert.Equal(await GlitchTipSourceBundle.ChecksumAsync(source, default), expected.Checksum);
        Assert.Equal(await GlitchTipSourceBundle.ChecksumAsync(map, default), expected.SourceMapChecksum);
        Assert.Equal(id, expected.DebugId);
        using var zip = new ZipArchive(bundle.Stream, ZipArchiveMode.Read, leaveOpen: true);
        using var manifest = await JsonDocument.ParseAsync(zip.GetEntry("manifest.json")!.Open());
        Assert.Equal("v2", manifest.RootElement.GetProperty("release").GetString());
        var entries = manifest.RootElement.GetProperty("files");
        Assert.Equal("source_map", entries.GetProperty("files/0/source.js.map").GetProperty("type").GetString());
        var sourceHeaders = entries.GetProperty("files/0/source.js").GetProperty("headers");
        Assert.Equal(expected.SourceMapChecksum, sourceHeaders.GetProperty("x-aspire-sourcemap-sha1").GetString());
        Assert.Equal(id, sourceHeaders.GetProperty("debug-id").GetString());
    }

    [Fact]
    public async Task SourceBundleRejectsUnpreparedArtifactsWithoutChangingThem()
    {
        using var files = new ArtifactDirectory();
        var source = files.Write("app.js", "throw new Error('sample');");
        var before = await File.ReadAllBytesAsync(source);
        await Assert.ThrowsAsync<DistributedApplicationException>(() => GlitchTipSourceBundle.CreateAsync(files.Path, "org", "project", "v2", default));
        Assert.Equal(before, await File.ReadAllBytesAsync(source));
    }

    [Fact]
    public async Task MissingOptionalRegistrationPerformsNoRemoteCalls()
    {
        using var files = new ArtifactDirectory();
        using var handler = new Handler(_ => throw new InvalidOperationException("No API call expected"));
        using var http = new HttpClient(handler);
        await UploadAsync(http, files.Path, [new("missing", "v1", true, GlitchTipArtifactKind.DebugSymbols)]);
        await Assert.ThrowsAsync<DistributedApplicationException>(() => UploadAsync(http, files.Path, [new("missing", "v1", false, GlitchTipArtifactKind.DebugSymbols)]));
    }

    [Fact]
    public async Task ResponseBodyIsNeverExposedInFailure()
    {
        using var files = new ArtifactDirectory();
        var path = files.Write("app.pdb", "BSJBtest");
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("test-token private response data") }));
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() => UploadAsync(http, files.Path, [new(path, "v1", false, GlitchTipArtifactKind.DebugSymbols)]));
        Assert.Contains("401", exception.Message);
        Assert.DoesNotContain("test-token", exception.ToString());
        Assert.DoesNotContain("private response data", exception.ToString());
    }

    [Theory]
    [InlineData("error")]
    [InlineData("not_found")]
    [InlineData("unexpected")]
    public void RejectedOrUnknownAssemblyBlocksDeployment(string state)
    {
        using var response = JsonDocument.Parse(JsonSerializer.Serialize(new { state, missingChunks = Array.Empty<string>() }));
        Assert.Throws<DistributedApplicationException>(() => GlitchTipArtifactUploader.EnsureAssemblyAccepted(response.RootElement));
    }

    [Fact]
    public async Task SourceUploadVerifiesAssembledContentAndSkipsIdenticalRetry()
    {
        using var files = new ArtifactDirectory();
        const string id = "ad0c0274-68bd-4ef6-801a-77d67f9894ed";
        var source = files.Write("app.js", $"throw new Error('sample');\n//# debugId={id}\n//# sourceMappingURL=app.js.map\n");
        var map = files.Write("app.js.map", $$"""{"version":3,"debug_id":"{{id}}","sources":["source.ts"],"mappings":"AAAA"}""");
        var sourceChecksum = await GlitchTipSourceBundle.ChecksumAsync(source, default);
        var mapChecksum = await GlitchTipSourceBundle.ChecksumAsync(map, default);
        var assembled = false;
        var uploads = 0;
        using var handler = new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("releases/", StringComparison.Ordinal)) return Json(new { });
            if (path.EndsWith("files/", StringComparison.Ordinal))
            {
                return Json(assembled ? new[] { new { sha1 = sourceChecksum, headers = new Dictionary<string, string>
                {
                    ["debug-id"] = id,
                    ["x-aspire-sourcemap-sha1"] = mapChecksum
                } } } : []);
            }
            if (path.EndsWith("chunk-upload/", StringComparison.Ordinal))
            {
                if (request.Method == HttpMethod.Get) return ChunkSettings();
                uploads++;
                var content = Assert.IsType<MultipartFormDataContent>(request.Content);
                using var compressed = await Assert.Single(content).ReadAsStreamAsync();
                using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                using var raw = new MemoryStream();
                await gzip.CopyToAsync(raw);
                raw.Position = 0;
                using var zip = new ZipArchive(raw, ZipArchiveMode.Read);
                using var manifest = await JsonDocument.ParseAsync(zip.GetEntry("manifest.json")!.Open());
                Assert.Equal("v2", manifest.RootElement.GetProperty("release").GetString());
                Assert.Equal("project", manifest.RootElement.GetProperty("project").GetString());
                return Json(new { });
            }
            Assert.EndsWith("artifactbundle/assemble/", path);
            var body = await BodyAsync(request);
            Assert.Equal("v2", body.GetProperty("version").GetString());
            Assert.Equal("project", Assert.Single(body.GetProperty("projects").EnumerateArray()).GetString());
            assembled = true;
            return Json(new { state = "created", missingChunks = Array.Empty<string>() });
        });
        using var http = new HttpClient(handler);
        var artifacts = new GlitchTipArtifact[] { new(files.Path, "v2", false, GlitchTipArtifactKind.SourceMaps) };
        await UploadAsync(http, files.Path, artifacts);
        Assert.Equal(1, uploads);
        await UploadAsync(http, files.Path, artifacts);
        Assert.Equal(1, uploads);
    }

    [Fact]
    public async Task SourceBundleRejectsDuplicateDebugIds()
    {
        using var files = new ArtifactDirectory();
        const string id = "ad0c0274-68bd-4ef6-801a-77d67f9894ed";
        foreach (var name in new[] { "first", "second" })
        {
            files.Write(name + ".js", $"//# debugId={id}\n//# sourceMappingURL={name}.js.map\n");
            files.Write(name + ".js.map", $$"""{"version":3,"debug_id":"{{id}}"}""");
        }
        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() => GlitchTipSourceBundle.CreateAsync(files.Path, "org", "project", "v1", default));
        Assert.Contains("unique debug ID", exception.Message);
    }

    [Fact]
    public async Task SourceBundleRejectsMalformedMap()
    {
        using var files = new ArtifactDirectory();
        files.Write("app.js", "//# debugId=ad0c0274-68bd-4ef6-801a-77d67f9894ed\n//# sourceMappingURL=app.js.map\n");
        files.Write("app.js.map", "not json");
        await Assert.ThrowsAsync<DistributedApplicationException>(() => GlitchTipSourceBundle.CreateAsync(files.Path, "org", "project", "v1", default));
    }


    [Fact]
    public async Task UploadDoesNotForwardCredentialsToAdvertisedExternalOrigin()
    {
        using var files = new ArtifactDirectory();
        var path = files.Write("app.pdb", "BSJBportable-pdb-test-fixture");
        using var handler = new Handler(request =>
        {
            Assert.Equal("glitchtip.example", request.RequestUri!.Host);
            if (request.RequestUri.AbsolutePath.EndsWith("files/dsyms/", StringComparison.Ordinal)) return Task.FromResult(Json(Array.Empty<object>()));
            if (request.RequestUri.AbsolutePath.EndsWith("chunk-upload/", StringComparison.Ordinal))
            {
                return Task.FromResult(Json(new { url = "https://unrelated.example/upload", hashAlgorithm = "sha1", compression = GzipCompression }));
            }
            return Task.FromResult(Json(new { }));
        });
        using var http = new HttpClient(handler);
        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() => UploadAsync(http, files.Path, [new(path, "v1", false, GlitchTipArtifactKind.DebugSymbols)]));
        Assert.Contains("outside the configured instance origin", exception.Message);
    }
    [Theory]
    [InlineData("CAFEBABE", false)]
    [InlineData("BEBAFECA", false)]
    [InlineData("CAFEBABF", false)]
    [InlineData("BFBAFECA", false)]
    [InlineData("CAFEBABE", true)]
    [InlineData("BEBAFECA", true)]
    [InlineData("CAFEBABF", true)]
    [InlineData("BFBAFECA", true)]
    public async Task UniversalMachOBlocksTheWholeRegistrationBeforeRemoteChanges(string magic, bool optional)
    {
        using var files = new ArtifactDirectory();
        files.Write("first.pdb", "BSJBportable-pdb-test-fixture");
        await File.WriteAllBytesAsync(Path.Combine(files.Path, "universal.dwarf"), Convert.FromHexString(magic + "00000002"));
        using var handler = new Handler(_ => throw new InvalidOperationException("Unsupported archives must fail before any remote changes."));
        using var http = new HttpClient(handler);

        var exception = await Assert.ThrowsAsync<DistributedApplicationException>(() => UploadAsync(http, files.Path,
            [new(files.Path, "v1", optional, GlitchTipArtifactKind.DebugSymbols)]));

        Assert.Contains("GlitchTip 6.2.6", exception.Message);
        Assert.Contains("separate thin debug file for each architecture", exception.Message);
    }

    [Theory]
    [InlineData("FEEDFACE")]
    [InlineData("CEFAEDFE")]
    [InlineData("FEEDFACF")]
    [InlineData("CFFAEDFE")]
    public async Task ThinMachOFormatsRemainEligibleForServerVerification(string magic)
    {
        using var files = new ArtifactDirectory();
        var path = Path.Combine(files.Path, "thin.dwarf");
        await File.WriteAllBytesAsync(path, Convert.FromHexString(magic + "00000000"));

        Assert.True(GlitchTipArtifactUploader.IsNativeDebugFile(path));
    }
    private static Task UploadAsync(HttpClient http, string directory, IReadOnlyList<GlitchTipArtifact> artifacts, TimeSpan? timeout = null) =>
        GlitchTipArtifactUploader.UploadAsync(http, new Uri("https://glitchtip.example/"), "test-token", "org", "project", artifacts, directory, TimeSpan.FromMilliseconds(1), timeout ?? TimeSpan.FromSeconds(10), default);


    private static HttpResponseMessage ChunkSettings(int size = 1024 * 1024) => Json(new
    {
        url = "/api/0/organizations/org/chunk-upload/",
        hashAlgorithm = "sha1",
        compression = GzipCompression,
        chunkSize = size,
        maxFileSize = 2147483648L
    });

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    private static async Task<JsonElement> BodyAsync(HttpRequestMessage request)
    {
        using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handler(request);
    }

    private sealed class ArtifactDirectory : IDisposable
    {
        internal string Path { get; } = Directory.CreateTempSubdirectory("glitchtip-artifact-test-").FullName;
        internal string Write(string name, string content)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
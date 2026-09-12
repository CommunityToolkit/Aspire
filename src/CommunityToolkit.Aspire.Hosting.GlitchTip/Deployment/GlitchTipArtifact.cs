// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;

internal enum GlitchTipArtifactKind { SourceMaps, DebugSymbols }

internal sealed record GlitchTipArtifact(string Path, string Release, bool Optional, GlitchTipArtifactKind Kind);

internal sealed record GlitchTipExpectedArtifact(string Name, string Checksum, string? DebugId = null, string? SourceMapChecksum = null);

internal sealed partial class GlitchTipSourceBundle : IAsyncDisposable
{
    private readonly string directory;
    internal FileStream Stream { get; }
    internal IReadOnlyList<GlitchTipExpectedArtifact> Expected { get; }

    private GlitchTipSourceBundle(string directory, FileStream stream, IReadOnlyList<GlitchTipExpectedArtifact> expected)
    {
        this.directory = directory;
        Stream = stream;
        Expected = expected;
    }

    internal static async Task<GlitchTipSourceBundle> CreateAsync(string path, string organization, string project, string release, CancellationToken cancellationToken)
    {
        var sourceFiles = Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).Where(IsJavaScript).Order(StringComparer.Ordinal).ToArray()
            : IsJavaScript(path) ? [path] : [];
        if (sourceFiles.Length == 0)
        {
            throw new DistributedApplicationException("A GlitchTip source-map registration must contain JavaScript files and their prepared source maps.");
        }

        var directory = Directory.CreateTempSubdirectory("aspire-glitchtip-").FullName;
        var stream = new FileStream(System.IO.Path.Combine(directory, "bundle.zip"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            var manifestFiles = new Dictionary<string, object>(StringComparer.Ordinal);
            var expected = new List<GlitchTipExpectedArtifact>();
            var debugIds = new HashSet<Guid>();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in sourceFiles)
                {
                    var source = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    var idMatch = DebugIdPattern().Match(source);
                    var mapMatch = SourceMapReferencePattern().Matches(source).LastOrDefault();
                    if (!idMatch.Success || !Guid.TryParse(idMatch.Groups[1].Value, out var debugId) || mapMatch is null)
                    {
                        throw new DistributedApplicationException("GlitchTip source maps require build-time debug-ID injection and a local sourceMappingURL. Run 'glitchtip-cli sourcemaps inject' during the build before registering these artifacts.");
                    }

                    var mapReference = mapMatch.Groups[1].Value;
                    if (Uri.TryCreate(mapReference, UriKind.Absolute, out _) || mapReference.Contains('?') || mapReference.Contains('#'))
                    {
                        throw new DistributedApplicationException("GlitchTip source-map registrations require a local relative sourceMappingURL without a query or fragment.");
                    }

                    var mapPath = System.IO.Path.GetFullPath(mapReference, System.IO.Path.GetDirectoryName(file)!);
                    var root = Directory.Exists(path) ? System.IO.Path.GetFullPath(path) : System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
                    var relativeMapPath = System.IO.Path.GetRelativePath(root, mapPath);
                    if (relativeMapPath == ".." || relativeMapPath.StartsWith(".." + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal) || System.IO.Path.IsPathRooted(relativeMapPath))
                    {
                        throw new DistributedApplicationException("A sourceMappingURL must remain inside its explicitly registered artifact directory.");
                    }

                    await using var map = File.OpenRead(mapPath);
                    using var mapJson = await JsonDocument.ParseAsync(map, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var mapId = mapJson.RootElement.TryGetProperty("debug_id", out var value) ? value.GetString()
                        : mapJson.RootElement.TryGetProperty("debugId", out value) ? value.GetString() : null;
                    if (!Guid.TryParse(mapId, out var parsedMapId) || parsedMapId != debugId || !debugIds.Add(debugId))
                    {
                        throw new DistributedApplicationException("Each GlitchTip source/map pair must have one matching, unique debug ID. Prepare the final build output before deployment.");
                    }

                    var sourceChecksum = await ChecksumAsync(file, cancellationToken).ConfigureAwait(false);
                    var mapChecksum = await ChecksumAsync(mapPath, cancellationToken).ConfigureAwait(false);
                    var index = expected.Count;
                    var sourceEntry = $"files/{index}/source.js";
                    var mapEntry = $"files/{index}/source.js.map";
                    var sourceName = System.IO.Path.GetFileName(file);
                    var mapName = $"{debugId:D}.js.map";
                    manifestFiles[sourceEntry] = new
                    {
                        type = "minified_source",
                        url = $"~/{sourceName}",
                        headers = new Dictionary<string, string>
                        {
                            ["debug-id"] = debugId.ToString("D"),
                            ["sourcemap"] = mapName,
                            // The read API returns the source bundle's headers, but not its map checksum.
                            // This marker verifies that assembly published this exact source/map pair.
                            ["x-aspire-sourcemap-sha1"] = mapChecksum
                        }
                    };
                    manifestFiles[mapEntry] = new { type = "source_map", url = $"~/{mapName}", headers = new Dictionary<string, string> { ["debug-id"] = debugId.ToString("D") } };
                    await CopyEntryAsync(zip, sourceEntry, file, cancellationToken).ConfigureAwait(false);
                    await CopyEntryAsync(zip, mapEntry, mapPath, cancellationToken).ConfigureAwait(false);
                    expected.Add(new(sourceName, sourceChecksum, debugId.ToString("D"), mapChecksum));
                }

                var entry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                await using var manifest = entry.Open();
                await JsonSerializer.SerializeAsync(manifest, new { org = organization, project, release, files = manifestFiles }, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            stream.Position = 0;
            return new(directory, stream, expected);
        }
        catch (Exception exception)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            Directory.Delete(directory);
            if (exception is JsonException)
            {
                throw new DistributedApplicationException("A registered GlitchTip source map is not valid JSON. Rebuild and prepare the source maps before deployment.");
            }
            throw;
        }
    }

    private static bool IsJavaScript(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".js" or ".mjs" or ".cjs" or ".jsbundle" or ".bundle";

    private static async Task CopyEntryAsync(ZipArchive zip, string entryName, string path, CancellationToken cancellationToken)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using var target = entry.Open();
        await using var source = File.OpenRead(path);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string> ChecksumAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask DisposeAsync()
    {
        await Stream.DisposeAsync().ConfigureAwait(false);
        Directory.Delete(directory);
    }

    [GeneratedRegex(@"(?m)^\s*//[#@]\s*debugId\s*=\s*([a-fA-F0-9-]{36})\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex DebugIdPattern();

    [GeneratedRegex(@"(?m)^\s*//[#@]\s*sourceMappingURL\s*=\s*(\S+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceMapReferencePattern();
}
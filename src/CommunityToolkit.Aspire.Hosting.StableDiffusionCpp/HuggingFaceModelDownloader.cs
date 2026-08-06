using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

internal static class HuggingFaceModelDownloader
{
    private const long ProgressIntervalBytes = 256L * 1024 * 1024;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DownloadLocks = [];

    public static async Task DownloadAsync(
        BeforeResourceStartedEvent @event,
        StableDiffusionCppModelResource model,
        CancellationToken cancellationToken)
    {
        var modelsDirectory = model.Parent.ModelsDirectory;
        var targetPath = GetTargetPath(modelsDirectory, model.FileName);
        var partialPath = $"{targetPath}.part";
        var downloadLock = DownloadLocks.GetOrAdd(targetPath, _ => new SemaphoreSlim(1, 1));
        var logger = @event.Services
            .GetRequiredService<ResourceLoggerService>()
            .GetLogger(model);

        await downloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(modelsDirectory);
            Directory.CreateDirectory(Path.Combine(modelsDirectory, "loras"));
            Directory.CreateDirectory(Path.Combine(modelsDirectory, "upscalers"));
            Directory.CreateDirectory(model.Parent.OutputDirectory);

            if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
            {
                logger.LogInformation("Hugging Face model already exists at {ModelPath}.", targetPath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var token = model.Parent.HuggingFaceToken is null
                ? null
                : await model.Parent.HuggingFaceToken.GetValueAsync(cancellationToken).ConfigureAwait(false);

            var downloadUri = BuildDownloadUri(model.Repository, model.Revision, model.FileName);

            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    await DownloadOnceAsync(downloadUri, partialPath, token, logger, cancellationToken)
                        .ConfigureAwait(false);

                    File.Move(partialPath, targetPath, overwrite: true);
                    logger.LogInformation("Hugging Face model downloaded to {ModelPath}.", targetPath);
                    return;
                }
                catch (Exception exception) when (
                    attempt < 5 &&
                    exception is HttpRequestException or IOException)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    logger.LogWarning(
                        exception,
                        "Model download attempt {Attempt} failed. Retrying in {Delay}.",
                        attempt,
                        delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            downloadLock.Release();
        }
    }

    private static async Task DownloadOnceAsync(
        Uri downloadUri,
        string partialPath,
        string? token,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existingLength = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;

        using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUri);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (existingLength > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingLength, null);
            logger.LogInformation(
                "Resuming Hugging Face model download from {DownloadedBytes} bytes: {DownloadUri}",
                existingLength,
                downloadUri);
        }
        else
        {
            logger.LogInformation("Downloading Hugging Face model: {DownloadUri}", downloadUri);
        }

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var canAppend = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        var fileMode = canAppend ? FileMode.Append : FileMode.Create;
        var downloadedLength = canAppend ? existingLength : 0;
        var nextProgress = downloadedLength + ProgressIntervalBytes;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var destination = new FileStream(
            partialPath,
            fileMode,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            useAsync: true);

        var buffer = new byte[1024 * 1024];
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                .ConfigureAwait(false);
            downloadedLength += bytesRead;

            if (downloadedLength >= nextProgress)
            {
                logger.LogInformation(
                    "Downloaded {DownloadedMegabytes:N0} MiB.",
                    downloadedLength / 1024d / 1024d);
                nextProgress += ProgressIntervalBytes;
            }
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

        if (response.Content.Headers.ContentLength is { } responseLength)
        {
            var expectedLength = (canAppend ? existingLength : 0) + responseLength;
            if (downloadedLength != expectedLength)
            {
                throw new IOException(
                    $"Incomplete model download. Expected {expectedLength} bytes, received {downloadedLength} bytes.");
            }
        }
    }

    internal static string GetTargetPath(string modelsDirectory, string fileName)
    {
        var rootPath = Path.GetFullPath(modelsDirectory);
        var relativePath = fileName
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var targetPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));

        if (!targetPath.StartsWith(
            rootPath + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Model file path '{fileName}' escapes the models directory.");
        }

        return targetPath;
    }

    internal static Uri BuildDownloadUri(string repository, string revision, string fileName)
    {
        static string EscapePath(string value) =>
            string.Join(
                '/',
                value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

        return new Uri(
            $"https://huggingface.co/{EscapePath(repository)}/resolve/{Uri.EscapeDataString(revision)}/{EscapePath(fileName)}");
    }
}

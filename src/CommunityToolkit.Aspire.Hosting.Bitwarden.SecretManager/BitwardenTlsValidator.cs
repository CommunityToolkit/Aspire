using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager;

internal static class BitwardenTlsValidator
{
    public static async Task ValidateTlsCertDirAsync(
        BitwardenSecretManagerResource resource,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        string? tlsCertDir = Environment.GetEnvironmentVariable("SSL_CERT_DIR");
        string? tlsCertFile = Environment.GetEnvironmentVariable("SSL_CERT_FILE");

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("TLS trust environment: SSL_CERT_DIR='{TlsCertDir}'.", tlsCertDir);
            logger.LogDebug("TLS trust environment: SSL_CERT_FILE='{TlsCertFile}'.", tlsCertFile);
        }

        if (string.IsNullOrEmpty(tlsCertDir))
        {
            return;
        }

        foreach (string certDir in tlsCertDir.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Directory.Exists(certDir))
            {
                logger.LogWarning("SSL_CERT_DIR path '{CertDir}' does not exist.", certDir);
            }
        }

        string[] httpsUrls = [.. new[]
            {
                await resource.GetApiUrlAsync(cancellationToken).ConfigureAwait(false),
                await resource.GetIdentityUrlAsync(cancellationToken).ConfigureAwait(false)
            }
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps)
            .Distinct()];

        if (httpsUrls.Length == 0)
        {
            return;
        }

        using HttpClient httpClient = new();
        httpClient.Timeout = TimeSpan.FromSeconds(15);

        foreach (string url in httpsUrls)
        {
            await VerifyTlsTrustAsync(resource, url, httpClient, tlsCertDir, logger, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task VerifyTlsTrustAsync(
        BitwardenSecretManagerResource resource,
        string url,
        HttpClient httpClient,
        string tlsCertDir,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Verifying TLS trust for '{Url}'.", url);
        try
        {
            using HttpResponseMessage _ = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            logger.LogDebug("TLS trust verified for '{Url}'.", url);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError == HttpRequestError.SecureConnectionError)
        {
            logger.LogError(ex, "TLS certificate validation failed for '{Url}'.", url);
            throw new DistributedApplicationException(
                $"Bitwarden resource '{resource.Name}': TLS certificate validation failed for '{url}'. " +
                $"Verify that SSL_CERT_DIR ('{tlsCertDir}') contains a trusted CA certificate for this endpoint.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Non-TLS error connecting to '{Url}' during SSL_CERT_DIR validation. Skipping.", url);
        }
    }
}

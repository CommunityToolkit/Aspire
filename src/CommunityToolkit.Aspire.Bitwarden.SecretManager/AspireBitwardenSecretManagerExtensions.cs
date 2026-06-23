using Aspire;
using Bitwarden.Sdk;
using CommunityToolkit.Aspire.Bitwarden.SecretManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering <see cref="BitwardenClient"/>.
/// </summary>
public static class AspireBitwardenSecretManagerExtensions
{
    private const string ConfigurationSection = "Aspire:Bitwarden:SecretManager";

    /// <summary>
    /// Registers a <see cref="BitwardenClient"/> from structured Aspire configuration.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">The connection name under <c>Aspire:Bitwarden:SecretManager</c>.</param>
    /// <param name="configureSettings">Optional settings override callback.</param>
    /// <returns>The host application builder.</returns>
    public static void AddBitwardenSecretManagerClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<BitwardenSecretManagerClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        AddBitwardenSecretManagerClient(builder, $"{ConfigurationSection}:{connectionName}", configureSettings, connectionName, serviceKey: null);
    }

    /// <summary>
    /// Registers a keyed <see cref="BitwardenClient"/> from structured Aspire configuration.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="name">The connection name under <c>Aspire:Bitwarden:SecretManager</c>. The same value is also used as the DI service key.</param>
    /// <param name="configureSettings">Optional settings override callback.</param>
    public static void AddKeyedBitwardenSecretManagerClient(
        this IHostApplicationBuilder builder,
        string name,
        Action<BitwardenSecretManagerClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        AddBitwardenSecretManagerClient(builder, $"{ConfigurationSection}:{name}", configureSettings, connectionName: name, serviceKey: name);
    }

    private static void AddBitwardenSecretManagerClient(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        Action<BitwardenSecretManagerClientSettings>? configureSettings,
        string connectionName,
        string? serviceKey)
    {
        BitwardenSecretManagerClientSettings settings = new();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);
        configureSettings?.Invoke(settings);
        ValidateSettings(settings, connectionName);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(settings);
            builder.Services.AddSingleton(_ => CreateClient(settings));
        }
        else
        {
            builder.Services.AddKeyedSingleton<BitwardenSecretManagerClientSettings>(serviceKey, settings);
            builder.Services.AddKeyedSingleton<BitwardenClient>(serviceKey, (_, _) => CreateClient(settings));
        }

        RegisterHealthCheck(builder, settings, connectionName, serviceKey);
    }

    private static BitwardenClient CreateClient(BitwardenSecretManagerClientSettings settings)
    {
        BitwardenClient client = new(new BitwardenSettings
        {
            ApiUrl = settings.ApiUrl,
            IdentityUrl = settings.IdentityUrl
        });

        string authCacheFile = string.Empty;
        if (settings.AuthCacheDirectory is { Length: > 0 } authCacheDirectory)
        {
            Directory.CreateDirectory(authCacheDirectory);
            string tokenId = ParseTokenId(settings.AccessToken) ?? "auth-cache";
            authCacheFile = Path.Combine(authCacheDirectory, tokenId);
        }

        client.Auth.LoginAccessToken(settings.AccessToken, authCacheFile);
        return client;
    }

    // Access token format: 0.<uuid>.<secret>:<base64_key>
    // Returns the UUID component used as the auth cache filename, matching the AppHost convention.
    private static string? ParseTokenId(string accessToken)
    {
        ReadOnlySpan<char> span = accessToken.AsSpan();
        int firstDot = span.IndexOf('.');
        if (firstDot >= 0)
        {
            ReadOnlySpan<char> rest = span[(firstDot + 1)..];
            int secondDot = rest.IndexOf('.');
            if (secondDot >= 0 && Guid.TryParse(rest[..secondDot], out Guid tokenId))
            {
                return tokenId.ToString("D");
            }
        }
        return null;
    }

    private static void RegisterHealthCheck(
        IHostApplicationBuilder builder,
        BitwardenSecretManagerClientSettings settings,
        string connectionName,
        string? serviceKey)
    {
        if (settings.DisableHealthChecks)
        {
            return;
        }

        string healthCheckName = $"BitwardenSecretManager_{connectionName}";
        builder.TryAddHealthCheck(new HealthCheckRegistration(
            healthCheckName,
            serviceProvider => new BitwardenSecretManagerHealthCheck(
                serviceKey is null
                    ? serviceProvider.GetRequiredService<BitwardenClient>()
                    : serviceProvider.GetRequiredKeyedService<BitwardenClient>(serviceKey),
                serviceKey is null
                    ? serviceProvider.GetRequiredService<BitwardenSecretManagerClientSettings>()
                    : serviceProvider.GetRequiredKeyedService<BitwardenSecretManagerClientSettings>(serviceKey)),
            failureStatus: default,
            tags: default,
            timeout: settings.HealthCheckTimeout));
    }

    private static void ValidateSettings(BitwardenSecretManagerClientSettings settings, string connectionName)
    {
        if (settings.OrganizationId == Guid.Empty)
        {
            throw new InvalidOperationException($"Bitwarden client connection '{connectionName}' is missing a valid organization identifier.");
        }

        if (settings.ProjectId == Guid.Empty)
        {
            throw new InvalidOperationException($"Bitwarden client connection '{connectionName}' is missing a valid project identifier.");
        }

        if (string.IsNullOrWhiteSpace(settings.AccessToken))
        {
            throw new InvalidOperationException($"Bitwarden client connection '{connectionName}' is missing an access token.");
        }

        ValidateAbsoluteUri(settings.ApiUrl, nameof(settings.ApiUrl), connectionName);
        ValidateAbsoluteUri(settings.IdentityUrl, nameof(settings.IdentityUrl), connectionName);
    }

    private static void ValidateAbsoluteUri(string value, string propertyName, string connectionName)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException($"Bitwarden client connection '{connectionName}' has an invalid {propertyName} value.");
        }
    }
}
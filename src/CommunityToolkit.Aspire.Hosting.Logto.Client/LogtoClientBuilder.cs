using Logto.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Logto.Client;

/// <summary>
/// Provides methods to configure and add Logto client services to an application builder.
/// </summary>
public static class LogtoClientBuilder
{
    private const string DefaultConfigSectionName = "Aspire:Logto:Client";


    /// <summary>
    /// Adds Logto client configuration and services to the application builder.
    /// </summary>
    /// <param name="builder">
    /// The <see cref="IHostApplicationBuilder"/> used to configure the application.
    /// </param>
    /// <param name="connectionName">
    /// The name of the connection string to retrieve the Logto endpoint from
    /// (optional, default is null).
    /// </param>
    /// <param name="configurationSectionName">
    /// The name of the configuration section containing Logto settings
    /// (optional, default is "Aspire:Logto:Client").
    /// </param>
    /// <param name="configureSettings">
    /// An action to configure additional <see cref="LogtoOptions"/> settings (optional).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the configuration lacks a valid Logto Endpoint or AppId.
    /// </exception>
    public static void AddLogtoSDKClient(this IHostApplicationBuilder builder,
        string? connectionName = null,
        string? configurationSectionName = DefaultConfigSectionName,
        Action<LogtoOptions>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new LogtoOptions();

        var sectionName = configurationSectionName ?? DefaultConfigSectionName;
        builder.Configuration.GetSection(sectionName).Bind(options);

        if (!string.IsNullOrEmpty(connectionName) &&
            builder.Configuration.GetConnectionString(connectionName) is { } cs)
        {
            var endpointFromCs = LogtoConnectionStringHelper.GetEndpointFromConnectionString(cs);

            if (!string.IsNullOrWhiteSpace(endpointFromCs) &&
                string.IsNullOrWhiteSpace(options.Endpoint))
            {
                options.Endpoint = endpointFromCs;
            }
        }
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new InvalidOperationException(
                $"Logto Endpoint must be configured in configuration section '{sectionName}' or via configureOptions.");

        builder.Services.AddLogtoAuthentication(opt =>
        {
            opt.Endpoint = options.Endpoint;
            opt.AppId = options.AppId;
            opt.AppSecret = options.AppSecret;
            configureSettings?.Invoke(opt);
            
            if (string.IsNullOrWhiteSpace(opt.Endpoint))
                throw new InvalidOperationException("Logto Endpoint must be configured.");

            if (string.IsNullOrEmpty(opt.AppId))
                throw new InvalidOperationException("Logto AppId must be configured.");
        });
    }
}
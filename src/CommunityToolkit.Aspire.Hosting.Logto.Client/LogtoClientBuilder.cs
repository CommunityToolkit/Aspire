using Logto.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CommunityToolkit.Aspire.Hosting.Logto.Client;

/// <summary>
/// Provides methods to configure and add Logto client services to an application builder.
/// </summary>
public static class LogtoClientBuilder
{
    private const string DefaultConfigSectionName = "Aspire:Logto:Client";


    /// <summary>
    /// Configures and adds the Logto OpenID Connect (OIDC) authentication for the specified application's service collection.
    /// </summary>
    /// <param name="builder">The application builder used to configure the application's services and pipeline.</param>
    /// <param name="connectionName">The name of the connection configuration to be used. If null, a default connection is used.</param>
    /// <param name="configurationSectionName">
    /// The name of the configuration section that contains Logto client settings.
    /// Defaults to "Aspire:Logto:Client".
    /// </param>
    /// <param name="authenticationScheme">
    /// The authentication scheme identifier for the Logto OIDC authentication. Defaults to "Logto".
    /// </param>
    /// <param name="cookieScheme">
    /// The cookie scheme name to be used with the Logto OIDC authentication. Defaults to "Logto.Cookie".
    /// </param>
    /// <param name="logtoOptions">
    /// A delegate to configure Logto-specific options such as endpoint, application ID, or secret.
    /// </param>
    /// <param name="oidcOptions">
    /// A delegate to configure OpenID Connect options for fine-tuning authentication behavior.
    /// </param>
    /// <returns>
    /// An updated <see cref="IServiceCollection"/> instance with the configured Logto OIDC authentication.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
    public static IServiceCollection AddLogtoOIDC(this IHostApplicationBuilder builder,
        string? connectionName = null,
        string? configurationSectionName = DefaultConfigSectionName,
        string authenticationScheme = "Logto",
        string cookieScheme = "Logto.Cookie",
        Action<LogtoOptions>? logtoOptions = null,
        Action<OpenIdConnectOptions>? oidcOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = GetEndpoint(builder.Configuration, configurationSectionName, connectionName);

        builder.Services.AddLogtoAuthentication(authenticationScheme, cookieScheme, opt =>
        {
            opt.Endpoint = options.Endpoint;
            opt.AppId = options.AppId;
            opt.AppSecret = options.AppSecret;
            logtoOptions?.Invoke(opt);
        });
        builder.Services.Configure<OpenIdConnectOptions>(authenticationScheme, opt =>
        {
            oidcOptions?.Invoke(opt);
        });
        return builder.Services;
    }

    /// <summary>
    /// Configures and adds the Logto JSON Web Token (JWT) Bearer authentication to the specified authentication builder.
    /// </summary>
    /// <param name="builder">The authentication builder used to configure authentication services.</param>
    /// <param name="serviceName">The name of the Logto service instance to be used for authentication.</param>
    /// <param name="appIdentification">A collection of application identifiers associated with the Logto service.</param>
    /// <param name="authenticationScheme">
    /// The authentication scheme identifier for the Logto JWT Bearer authentication. Defaults to "Bearer".
    /// </param>
    /// <param name="configurationSectionName">
    /// The name of the configuration section that contains Logto client settings. Defaults to "Aspire:Logto:Client".
    /// </param>
    /// <param name="configureOptions">
    /// A delegate to configure the options for JWT bearer authentication.
    /// </param>
    /// <returns>
    /// An updated <see cref="AuthenticationBuilder"/> instance with the configured Logto JWT Bearer authentication.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder or serviceName is null.</exception>
    public static AuthenticationBuilder AddLogtoJwtBearer(this AuthenticationBuilder builder,
        string serviceName,
        string appIdentification,
        string authenticationScheme = JwtBearerDefaults.AuthenticationScheme,
        string? configurationSectionName = DefaultConfigSectionName,
        Action<JwtBearerOptions>? configureOptions = null)
    {
        return AddLogtoJwtBearer(builder, serviceName, [appIdentification], authenticationScheme,
            configurationSectionName, configureOptions);
    }


    /// <summary>
    /// Configures and adds the Logto JSON Web Token (JWT) Bearer authentication for the specified application.
    /// </summary>
    /// <param name="builder">
    /// The authentication builder used to configure the application's authentication services.
    /// </param>
    /// <param name="serviceName">
    /// The name of the service associated with Logto configuration.
    /// </param>
    /// <param name="appIdentification">
    /// A collection of application identifiers (audiences) used to validate the JWT's audience claim.
    /// </param>
    /// <param name="authenticationScheme">
    /// The authentication scheme identifier for the Logto JWT Bearer authentication. Defaults to "Bearer".
    /// </param>
    /// <param name="configurationSectionName">
    /// The name of the configuration section that contains Logto client settings. Defaults to "Aspire:Logto:Client".
    /// </param>
    /// <param name="configureOptions">
    /// A delegate to configure additional JwtBearerOptions as needed for specific authentication behavior.
    /// </param>
    /// <returns>
    /// An updated <see cref="AuthenticationBuilder"/> instance with the configured Logto JWT Bearer authentication.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when serviceName or appIndeficator is missing or invalid.
    /// </exception>
    public static AuthenticationBuilder AddLogtoJwtBearer(this AuthenticationBuilder builder,
        string serviceName,
        IEnumerable<string> appIdentification,
        string authenticationScheme = JwtBearerDefaults.AuthenticationScheme,
        string? configurationSectionName = DefaultConfigSectionName,
        Action<JwtBearerOptions>? configureOptions = null)
    {
        builder.Services
            .AddOptions<JwtBearerOptions>(authenticationScheme)
            .Configure<IConfiguration>((jwt, configuration) =>
            {
                var logto = GetEndpoint(configuration, configurationSectionName, serviceName);
                var issuer = logto.Endpoint.TrimEnd('/') + "/oidc";

                jwt.Authority = issuer;
                jwt.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudiences = appIdentification
                };
                configureOptions?.Invoke(jwt);
            });
        builder.AddJwtBearer(authenticationScheme);
        return builder;
    }

    private static LogtoOptions GetEndpoint(IConfiguration configuration, string? configurationSectionName,
        string? connectionName)
    {
        var options = new LogtoOptions();

        var sectionName = configurationSectionName ?? DefaultConfigSectionName;
        configuration.GetSection(sectionName).Bind(options);

        if (!string.IsNullOrEmpty(connectionName) &&
            configuration.GetConnectionString(connectionName) is { } cs)
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

        return options;
    }
}
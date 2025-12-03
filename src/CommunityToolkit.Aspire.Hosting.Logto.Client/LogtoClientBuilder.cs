using Logto.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Logto.Client;

/// <summary>
/// Provides methods to configure and add Logto client services to an application builder.
/// </summary>
public static class LogtoClientBuilder
{
    private const string DefaultConfigSectionName = "Aspire:Logto:Client";

    /// <summary>
    /// Configures and adds the Logto SDK client to the specified application's service collection.
    /// </summary>
    /// <param name="builder">The application builder that is used to configure the application.</param>
    /// <param name="connectionName">The optional name of the connection string from the configuration to be used for Logto.</param>
    /// <param name="configurationSectionName">The name of the configuration section that contains Logto settings. Defaults to "Aspire:Logto:Client".</param>
    /// <param name="authenticationScheme">The name of the authentication scheme used for Logto. Defaults to "Logto".</param>
    /// <param name="cookieScheme">The name of the cookie scheme used for Logto. Defaults to "Logto.Cookie".</param>
    /// <param name="logtoOptions">A delegate to configure settings specific to Logto authentication.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Logto "Endpoint" configuration is not specified or invalid.
    /// </exception>
    public static IServiceCollection AddLogtoOIDC(this IHostApplicationBuilder builder,
        string? connectionName = null,
        string? configurationSectionName = DefaultConfigSectionName,
        string authenticationScheme = "Logto",
        string cookieScheme = "Logto.Cookie",
        Action<LogtoOptions>? logtoOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(logtoOptions);

        var options = GetEndpoint(builder.Configuration, configurationSectionName, connectionName);
        
        return builder.Services.AddLogtoAuthentication(authenticationScheme, cookieScheme, opt =>
        {
            opt.Endpoint = options.Endpoint;
            opt.AppId = options.AppId;
            opt.AppSecret = options.AppSecret;
            logtoOptions?.Invoke(opt);
        });
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="serviceName"></param>
    /// <param name="authenticationScheme"></param>
    /// <param name="configurationSectionName"></param>
    /// <param name="configureOptions"></param>
    /// <returns></returns>
    public static AuthenticationBuilder AddLogtoJwtBearer(this AuthenticationBuilder builder,
        string serviceName,
        string authenticationScheme,
        string? configurationSectionName = DefaultConfigSectionName,
        Action<JwtBearerOptions>? configureOptions = null)
    {
        
        builder.Services
            .AddOptions<JwtBearerOptions>(authenticationScheme)
            .Configure<IConfiguration>((jwt, configuration) =>
            {
                var logto = GetEndpoint(configuration, configurationSectionName, serviceName);

                jwt.Authority = logto.Endpoint.TrimEnd('/') + "/oidc";
                jwt.Audience  = logto.AppId;

                // dev-хак: если Logto по http и мы в dev-окружении — можно ещё сюда
                // добавить RequireHttpsMetadata = false через отдельный параметр,
                // но окружение лучше проверять в другом экстеншене.

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
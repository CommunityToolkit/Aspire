using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Postgres;
using CommunityToolkit.Aspire.Hosting.Supabase;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System;
using System.Data;

// ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Supabase resources to the application model.
/// </summary>
public static class SupabaseBuilderExtensions
{
    private const int SupabaseApiPort = 8000;
    private const int PostgresDatabasePort = 5432;

    /// <summary>
    /// Adds a Supabase meta resource, with essential components like Postgres and HTTP API, to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Supabase resource.</param>
    /// <param name="databasePort"></param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> AddSupabase(
        this IDistributedApplicationBuilder builder,
        string name, int? databasePort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        SupabaseResource resource = new(name);

        //Create default parameters if not provided

        resource.DashboardPasswordParameter ??=
            ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");

        resource.DatabasePasswordParameter ??=
            ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-db-password");

        IResourceBuilder<SupabaseResource> metaBuilder = builder.AddResource(resource);

        #region Postgres
        
        // separate Postgres database container
        metaBuilder.WithModule("postgres", SupabaseContainerImageTags.PostgresImage, SupabaseContainerImageTags.PostgresTag)
            .WithEndpoint(targetPort: PostgresDatabasePort, port: databasePort,
                name: SupabaseResource.DatabaseEndpointName) //TODO check this
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["POSTGRES_PASSWORD"] = resource.DatabasePasswordParameter;
                context.EnvironmentVariables["POSTGRES_DB"] = "postgres";
                context.EnvironmentVariables["POSTGRES_USER"] = "postgres";
                context.EnvironmentVariables["PGDATA"] = "/var/lib/postgresql/data";
                context.EnvironmentVariables["PGHOST"] = resource.DatabaseEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["PGPORT"] = resource.DatabaseEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables["PGPASSWORD"] = resource.DatabasePasswordParameter;
                context.EnvironmentVariables["PGDATABASE"] = resource.DatabaseNameParameter;
            })
            .WithParentRelationship(resource);

        string? connectionString = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(resource, async (@event, ct) =>
        {
            connectionString = await resource.GetConnectionStringAsync(ct).ConfigureAwait(false);

            if (connectionString == null)
            {
                throw new DistributedApplicationException(
                    $"ConnectionStringAvailableEvent was published for the '{resource.Name}' resource but the connection string was null.");
            }
        });

        builder.Eventing.Subscribe<ResourceReadyEvent>(resource, async (@event, ct) =>
        {
            if (connectionString is null)
            {
                throw new DistributedApplicationException(
                    $"ResourceReadyEvent was published for the '{resource.Name}' resource but the connection string was null.");
            }

            // Non-database scoped connection string
            await using NpgsqlConnection npgsqlConnection = new(connectionString + ";Database=postgres;");

            await npgsqlConnection.OpenAsync(ct).ConfigureAwait(false);

            if (npgsqlConnection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException($"Could not open connection to '{resource.Name}'");
            }

            /*foreach (var name in resource.Databases.Keys)
            {
                if (builder.Resources.FirstOrDefault(n => string.Equals(n.Name, name, StringComparisons.ResourceName)) is PostgresDatabaseResource postgreDatabase)
                {
                    await CreateDatabaseAsync(npgsqlConnection, postgreDatabase, @event.Services, ct).ConfigureAwait(false);
                }
            }*/
        });

        string healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks().AddNpgSql(
            sp => connectionString ?? throw new InvalidOperationException("Connection string is unavailable"),
            name: healthCheckKey, configure: (connection) =>
            {
                // HACK: The Npgsql client defaults to using the username in the connection string if the database is not specified. Here
                //       we override this default behavior because we are working with a non-database scoped connection string. The Aspirified
                //       package doesn't have to deal with this because it uses a datasource from DI which doesn't have this issue:
                //
                //       https://github.com/npgsql/npgsql/blob/c3b31c393de66a4b03fba0d45708d46a2acb06d2/src/Npgsql/NpgsqlConnection.cs#L445
                //
                connection.ConnectionString += ";Database=postgres;";
            });

        #endregion

        #region Kong

        metaBuilder.WithModule("kong", SupabaseContainerImageTags.KongImage, SupabaseContainerImageTags.KongTag)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["KONG_DATABASE"] = "off";
                context.EnvironmentVariables["KONG_DECLARATIVE_CONFIG"] = "/home/kong/kong.yml";
                context.EnvironmentVariables["KONG_DNS_ORDER"] = "LAST,A,CNAME";
                context.EnvironmentVariables["KONG_PLUGINS"] = "request-transformer,cors,key-auth,acl,basic-auth";
                context.EnvironmentVariables["KONG_NGINX_PROXY_PROXY_BUFFER_SIZE"] = "160k";
                context.EnvironmentVariables["KONG_NGINX_PROXY_PROXY_BUFFERS"] = "64 160k";
                context.EnvironmentVariables["SUPABASE_ANON_KEY"] =
                    resource.DashboardPasswordParameter; // Replace with actual anon key
                context.EnvironmentVariables["SUPABASE_SERVICE_KEY"] =
                    resource.DashboardPasswordParameter; // Replace with actual service role key
                context.EnvironmentVariables["DASHBOARD_USERNAME"] = "admin"; // Replace with actual dashboard username
                context.EnvironmentVariables["DASHBOARD_PASSWORD"] =
                    resource.DashboardPasswordParameter; // Replace with actual dashboard password
            })
            .WithHttpEndpoint(targetPort: 8000, port: null, name: $"{resource.Name}-kong");

        #endregion

        #region Studio
        
        metaBuilder.WithModule("studio", SupabaseContainerImageTags.StudioImage,
                SupabaseContainerImageTags.StudioTag)
            .WithEnvironment(context =>
            {
                // Meta service connection
                context.EnvironmentVariables["STUDIO_PG_META_URL"] = "http://meta:8080";
                context.EnvironmentVariables["POSTGRES_PASSWORD"] = resource.DashboardPasswordParameter;

                // Organization and project settings
                context.EnvironmentVariables["DEFAULT_ORGANIZATION_NAME"] = "Default Organization";
                context.EnvironmentVariables["DEFAULT_PROJECT_NAME"] = "Default Project";
                context.EnvironmentVariables["OPENAI_API_KEY"] = ""; // Optional

                // Supabase API connection
                context.EnvironmentVariables["SUPABASE_URL"] = "http://kong:8000";
                context.EnvironmentVariables["SUPABASE_PUBLIC_URL"] =
                    resource.PrimaryEndpoint.Property(EndpointProperty.Url);
                context.EnvironmentVariables["SUPABASE_ANON_KEY"] =
                    resource.DashboardPasswordParameter; // Using password as anon key
                context.EnvironmentVariables["SUPABASE_SERVICE_KEY"] =
                    resource.DashboardPasswordParameter; // Using password as service role key
                context.EnvironmentVariables["AUTH_JWT_SECRET"] = resource.DashboardPasswordParameter;

                // Logging configuration
                context.EnvironmentVariables["LOGFLARE_PRIVATE_ACCESS_TOKEN"] = "private_placeholder_token";
                context.EnvironmentVariables["LOGFLARE_URL"] = "http://analytics:4000";
                context.EnvironmentVariables["NEXT_PUBLIC_ENABLE_LOGS"] = "true";
                context.EnvironmentVariables["NEXT_ANALYTICS_BACKEND_PROVIDER"] = "postgres";
            })
            .WithHttpEndpoint(targetPort: 3000, port: null, name: $"{resource.Name}-studio");

        #endregion

        return metaBuilder;
    }

    #region Modules

    /// <summary>
    /// Adds all Supabase modules (child services) to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithAllModules(
        this IResourceBuilder<SupabaseResource> builder)
    {
        return builder
            .WithRest()
            .WithRealtime()
            .WithStorageService()
            .WithAuthService()
            .WithMetaService()
            .WithInbucketService()
            .WithImageProxyService()
            .WithLogflareService()
            .WithEdgeRuntimeService();
    }

    // Individual module extension methods
    private static IResourceBuilder<SupabaseResource> WithModule(
        this IResourceBuilder<SupabaseResource> builder,
        string suffix,
        string image,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(builder);
        SupabaseResource resource = builder.Resource;
        ContainerResource container = new ContainerResource($"{resource.Name}-{suffix}");
        builder.ApplicationBuilder.AddResource(container)
            .WithImage(image, tag)
            .WithImageRegistry(SupabaseContainerImageTags.Registry)
            .WithParentRelationship(resource);
        return builder;
    }

    /// <summary>
    /// Adds the REST module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithRest(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("rest", SupabaseContainerImageTags.RestImage, SupabaseContainerImageTags.RestTag)
            .WithEnvironment(context =>
            {
                SupabaseResource resource = builder.Resource;

                context.EnvironmentVariables["PGRST_DB_URI"] =
                    $"postgres://supabase_admin:{resource.DashboardPasswordParameter}@{resource.DatabaseEndpoint.Property(EndpointProperty.Host)}:{resource.DatabaseEndpoint.Property(EndpointProperty.Port)}/database";
                context.EnvironmentVariables["PGRST_DB_SCHEMA"] = "public";
                context.EnvironmentVariables["PGRST_DB_ANON_ROLE"] = "anon";
                context.EnvironmentVariables["PGRST_JWT_SECRET"] = resource.DashboardPasswordParameter;
                context.EnvironmentVariables["PGRST_DB_USE_LEGACY_GUCS"] = "false";
                //context.EnvironmentVariables["PGRST_OPENAPI_SERVER_HOST"] = resource.PrimaryEndpoint.Property(EndpointProperty.Host);
                //context.EnvironmentVariables["PGRST_OPENAPI_SERVER_PORT"] = resource.PrimaryEndpoint.Property(EndpointProperty.Port);

                context.EnvironmentVariables["PGRST_APP_SETTINGS_JWT_SECRET"] = resource.DashboardPasswordParameter;
                context.EnvironmentVariables["PGRST_APP_SETTINGS_JWT_EXP"] = "3600"; // 1 hour
            })
            .WithArgs("postgrest")
            .WithHttpEndpoint(targetPort: 3000, port: null, name: $"{builder.Resource.Name}-rest");
    }

    /// <summary>
    /// Adds the Realtime module to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithRealtime(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("realtime", SupabaseContainerImageTags.RealtimeImage,
                SupabaseContainerImageTags.RealtimeTag)
            .WithEnvironment(context =>
            {
                SupabaseResource resource = builder.Resource;

                // Basic configuration
                context.EnvironmentVariables["PORT"] = "4000";

                // Database connection
                context.EnvironmentVariables["DB_HOST"] = resource.DatabaseEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["DB_PORT"] = resource.DatabaseEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables["DB_USER"] = "supabase_admin";
                context.EnvironmentVariables["DB_PASSWORD"] = resource.DashboardPasswordParameter;
                context.EnvironmentVariables["DB_NAME"] = "postgres";
                context.EnvironmentVariables["DB_AFTER_CONNECT_QUERY"] = "SET search_path TO _realtime";
                context.EnvironmentVariables["DB_ENC_KEY"] = "supabaserealtime";

                // API configuration
                context.EnvironmentVariables["API_JWT_SECRET"] = resource.DashboardPasswordParameter;

                // Erlang settings
                context.EnvironmentVariables["SECRET_KEY_BASE"] = "supabase_realtime_secret_key_base";
                context.EnvironmentVariables["ERL_AFLAGS"] = "-proto_dist inet_tcp";
                context.EnvironmentVariables["DNS_NODES"] = "''";
                context.EnvironmentVariables["RLIMIT_NOFILE"] = "10000";

                // App settings
                context.EnvironmentVariables["APP_NAME"] = "realtime";
                context.EnvironmentVariables["SEED_SELF_HOST"] = "true";
                context.EnvironmentVariables["RUN_JANITOR"] = "true";
            })
            .WithHttpEndpoint(targetPort: 4000, port: null, name: $"{builder.Resource.Name}-realtime");
    }

    /// <summary>
    /// Adds the Storage module to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithStorageService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("storage", SupabaseContainerImageTags.StorageImage,
                SupabaseContainerImageTags.StorageTag)
            .WithEnvironment(context =>
            {
                SupabaseResource resource = builder.Resource;

                // API keys
                context.EnvironmentVariables["ANON_KEY"] = resource.DashboardPasswordParameter;
                context.EnvironmentVariables["SERVICE_KEY"] = resource.DashboardPasswordParameter;

                // API connections
                context.EnvironmentVariables["POSTGREST_URL"] = "http://rest:3000";
                context.EnvironmentVariables["PGRST_JWT_SECRET"] = resource.DashboardPasswordParameter;

                // Database connection
                context.EnvironmentVariables["DATABASE_URL"] =
                    $"postgres://supabase_storage_admin:{resource.DashboardPasswordParameter}@{resource.DatabaseEndpoint.Property(EndpointProperty.Host)}:{resource.DatabaseEndpoint.Property(EndpointProperty.Port)}/postgres";

                // Storage configuration
                context.EnvironmentVariables["FILE_SIZE_LIMIT"] = "52428800"; // 50MB
                context.EnvironmentVariables["STORAGE_BACKEND"] = "file";
                context.EnvironmentVariables["FILE_STORAGE_BACKEND_PATH"] = "/var/lib/storage";

                // Tenant settings
                context.EnvironmentVariables["TENANT_ID"] = "stub";
                context.EnvironmentVariables["REGION"] = "stub";
                context.EnvironmentVariables["GLOBAL_S3_BUCKET"] = "stub";

                // Image transformation
                context.EnvironmentVariables["ENABLE_IMAGE_TRANSFORMATION"] = "true";
                context.EnvironmentVariables["IMGPROXY_URL"] = "http://imgproxy:5001";
            })
            .WithBindMount("./volumes/storage", "/var/lib/storage")
            .WithHttpEndpoint(targetPort: 5000, port: null, name: $"{builder.Resource.Name}-storage");
    }

    /// <summary>
    /// Adds the Auth module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithAuthService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("auth", SupabaseContainerImageTags.AuthImage, SupabaseContainerImageTags.AuthTag)
            .WithEnvironment(context =>
            {
                SupabaseResource resource = builder.Resource;

                context.EnvironmentVariables["GOTRUE_API_HOST"] = "0.0.0.0";
                context.EnvironmentVariables["GOTRUE_API_PORT"] = "9999";
                context.EnvironmentVariables["API_EXTERNAL_URL"] =
                    resource.PrimaryEndpoint.Property(EndpointProperty.Url);

                context.EnvironmentVariables["GOTRUE_DB_DRIVER"] = "postgres";
                context.EnvironmentVariables["GOTRUE_DB_DATABASE_URL"] =
                    $"postgres://supabase_auth_admin:{resource.DashboardPasswordParameter}@{resource.DatabaseEndpoint.Property(EndpointProperty.Host)}:{resource.DatabaseEndpoint.Property(EndpointProperty.Port)}/database";

                context.EnvironmentVariables["GOTRUE_SITE_URL"] =
                    resource.PrimaryEndpoint.Property(EndpointProperty.Url);
                context.EnvironmentVariables["GOTRUE_URI_ALLOW_LIST"] =
                    resource.PrimaryEndpoint.Property(EndpointProperty.Url);
                context.EnvironmentVariables["GOTRUE_DISABLE_SIGNUP"] = "false"; // Set to true to disable signup

                context.EnvironmentVariables["GOTRUE_JWT_ADMIN_ROLES"] = "service_role";
                context.EnvironmentVariables["GOTRUE_JWT_AUD"] = "authenticated";
                context.EnvironmentVariables["GOTRUE_JWT_DEFAULT_GROUP_NAME"] = "authenticated";
                context.EnvironmentVariables["GOTRUE_JWT_EXP"] = "3600"; // 1 hour
                context.EnvironmentVariables["GOTRUE_JWT_SECRET"] = resource.DashboardPasswordParameter;

                context.EnvironmentVariables["GOTRUE_EXTERNAL_EMAIL_ENABLED"] = "true"; // Enable email signup
                context.EnvironmentVariables["GOTRUE_EXTERNAL_ANONYMOUS_USERS_ENABLED"] =
                    "true"; // Enable anonymous users
                context.EnvironmentVariables["GOTRUE_MAILER_AUTOCONFIRM"] =
                    "true"; // Automatically confirm email signups

                // SMTP configuration
                context.EnvironmentVariables["GOTRUE_SMTP_ADMIN_EMAIL"] = "admin@example.com";
                context.EnvironmentVariables["GOTRUE_SMTP_HOST"] = "smtp.example.com";
                context.EnvironmentVariables["GOTRUE_SMTP_PORT"] = "587";
                context.EnvironmentVariables["GOTRUE_SMTP_USER"] = "smtp_user";
                context.EnvironmentVariables["GOTRUE_SMTP_PASS"] = "smtp_password";
                context.EnvironmentVariables["GOTRUE_SMTP_SENDER_NAME"] = "Supabase";

                // URL paths for auth flows
                context.EnvironmentVariables["GOTRUE_MAILER_URLPATHS_INVITE"] = "/auth/v1/verify";
                context.EnvironmentVariables["GOTRUE_MAILER_URLPATHS_CONFIRMATION"] = "/auth/v1/verify";
                context.EnvironmentVariables["GOTRUE_MAILER_URLPATHS_RECOVERY"] = "/auth/v1/verify";
                context.EnvironmentVariables["GOTRUE_MAILER_URLPATHS_EMAIL_CHANGE"] = "/auth/v1/verify";

                // Phone authentication
                context.EnvironmentVariables["GOTRUE_EXTERNAL_PHONE_ENABLED"] =
                    "false"; // Disable phone signup by default
                context.EnvironmentVariables["GOTRUE_SMS_AUTOCONFIRM"] =
                    "false"; // Disable phone autoconfirm by default

                // Additional configurations from docker-compose
                // Uncomment and configure as needed
                // context.EnvironmentVariables["GOTRUE_EXTERNAL_SKIP_NONCE_CHECK"] = "true"; // Bypass nonce check in ID Token flow for mobile
                // context.EnvironmentVariables["GOTRUE_MAILER_SECURE_EMAIL_CHANGE_ENABLED"] = "true"; // Require confirmation for email change
                // context.EnvironmentVariables["GOTRUE_SMTP_MAX_FREQUENCY"] = "1s"; // Minimum time between emails

                // Hook configurations
                // Uncomment and configure as needed
                // context.EnvironmentVariables["GOTRUE_HOOK_CUSTOM_ACCESS_TOKEN_ENABLED"] = "true";
                // context.EnvironmentVariables["GOTRUE_HOOK_CUSTOM_ACCESS_TOKEN_URI"] = "pg-functions://postgres/public/custom_access_token_hook";
                // context.EnvironmentVariables["GOTRUE_HOOK_CUSTOM_ACCESS_TOKEN_SECRETS"] = "<base64-secret>";

                // context.EnvironmentVariables["GOTRUE_HOOK_MFA_VERIFICATION_ATTEMPT_ENABLED"] = "true";
                // context.EnvironmentVariables["GOTRUE_HOOK_MFA_VERIFICATION_ATTEMPT_URI"] = "pg-functions://postgres/public/mfa_verification_attempt";

                // context.EnvironmentVariables["GOTRUE_HOOK_PASSWORD_VERIFICATION_ATTEMPT_ENABLED"] = "true";
                // context.EnvironmentVariables["GOTRUE_HOOK_PASSWORD_VERIFICATION_ATTEMPT_URI"] = "pg-functions://postgres/public/password_verification_attempt";

                // context.EnvironmentVariables["GOTRUE_HOOK_SEND_SMS_ENABLED"] = "false";
                // context.EnvironmentVariables["GOTRUE_HOOK_SEND_SMS_URI"] = "pg-functions://postgres/public/custom_access_token_hook";
                // context.EnvironmentVariables["GOTRUE_HOOK_SEND_SMS_SECRETS"] = "v1,whsec_someBaseSecret";

                // context.EnvironmentVariables["GOTRUE_HOOK_SEND_EMAIL_ENABLED"] = "false";
                // context.EnvironmentVariables["GOTRUE_HOOK_SEND_EMAIL_URI"] = "http://host.docker.internal:54321/functions/v1/email_sender";
                // context.EnvironmentVariables["GOTRUE_HOOK_SEND_EMAIL_SECRETS"] = "v1,whsec_someBaseSecret";
            })
            .WithHttpEndpoint(targetPort: 9999, port: null, name: $"{builder.Resource.Name}-auth");
    }

    /// <summary>
    /// Adds the Meta module to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithMetaService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("meta", SupabaseContainerImageTags.MetaImage, SupabaseContainerImageTags.MetaTag)
            .WithEnvironment(context =>
            {
                SupabaseResource resource = builder.Resource;

                // Configuration
                context.EnvironmentVariables["PG_META_PORT"] = "8080";

                // Database connection
                context.EnvironmentVariables["PG_META_DB_HOST"] =
                    resource.DatabaseEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["PG_META_DB_PORT"] =
                    resource.DatabaseEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables["PG_META_DB_NAME"] = "postgres";
                context.EnvironmentVariables["PG_META_DB_USER"] = "supabase_admin";
                context.EnvironmentVariables["PG_META_DB_PASSWORD"] = resource.DashboardPasswordParameter;
            })
            .WithHttpEndpoint(targetPort: 8080, port: null, name: $"{builder.Resource.Name}-meta");
    }

    /// <summary>
    /// Adds the Inbucket module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithInbucketService(
        this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("inbucket", SupabaseContainerImageTags.InbucketImage,
                SupabaseContainerImageTags.InbucketTag)
            .WithHttpEndpoint(targetPort: 9000, port: null, name: $"{builder.Resource.Name}-inbucket");
    }

    /// <summary>
    /// Adds the Image Proxy module to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithImageProxyService(
        this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("image-proxy", SupabaseContainerImageTags.ImageProxyImage,
                SupabaseContainerImageTags.ImageProxyTag)
            .WithEnvironment(context =>
            {
                // Core settings
                context.EnvironmentVariables["IMGPROXY_BIND"] = ":5001";
                context.EnvironmentVariables["IMGPROXY_LOCAL_FILESYSTEM_ROOT"] = "/";
                context.EnvironmentVariables["IMGPROXY_USE_ETAG"] = "true";
                context.EnvironmentVariables["IMGPROXY_ENABLE_WEBP_DETECTION"] = "true";
            })
            /*.WithBindMount("./volumes/storage", "/var/lib/storage")*/
            .WithHttpEndpoint(targetPort: 5001, port: null, name: $"{builder.Resource.Name}-image-proxy");
    }

    /// <summary>
    /// Adds the Logflare module to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <param name="port">Optional host port for the Logflare service.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithLogflareService(
        this IResourceBuilder<SupabaseResource> builder,
        int? port = 4000)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string builderUniqueName = builder.Resource.Name;
        string endpointName = $"{builderUniqueName}-logflare";
        return builder.WithModule("logflare", SupabaseContainerImageTags.LogflareImage,
                SupabaseContainerImageTags.LogflareTag)
            .WithEnvironment(context =>
            {
                SupabaseResource resource = builder.Resource;

                // Core settings
                context.EnvironmentVariables["LOGFLARE_NODE_HOST"] = "127.0.0.1";
                context.EnvironmentVariables["LOGFLARE_SINGLE_TENANT"] = "true";
                context.EnvironmentVariables["LOGFLARE_SUPABASE_MODE"] = "true";
                context.EnvironmentVariables["LOGFLARE_MIN_CLUSTER_SIZE"] = "1";

                // Database connection
                context.EnvironmentVariables["DB_USERNAME"] = "supabase_admin";
                context.EnvironmentVariables["DB_DATABASE"] = "_supabase";
                context.EnvironmentVariables["DB_HOSTNAME"] = resource.DatabaseEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["DB_PORT"] = resource.DatabaseEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables["DB_PASSWORD"] = resource.DashboardPasswordParameter;
                context.EnvironmentVariables["DB_SCHEMA"] = "_analytics";

                // Access tokens
                context.EnvironmentVariables["LOGFLARE_PUBLIC_ACCESS_TOKEN"] = "public_placeholder_token";
                context.EnvironmentVariables["LOGFLARE_PRIVATE_ACCESS_TOKEN"] = "private_placeholder_token";

                // Postgres backend configuration (default)
                context.EnvironmentVariables["POSTGRES_BACKEND_URL"] =
                    $"postgresql://supabase_admin:{resource.DashboardPasswordParameter}@{resource.DatabaseEndpoint.Property(EndpointProperty.Host)}:{resource.DatabaseEndpoint.Property(EndpointProperty.Port)}/_supabase";
                context.EnvironmentVariables["POSTGRES_BACKEND_SCHEMA"] = "_analytics";
                context.EnvironmentVariables["LOGFLARE_FEATURE_FLAG_OVERRIDE"] = "multibackend=true";

                // BigQuery backend configuration (commented out by default)
                // context.EnvironmentVariables["GOOGLE_PROJECT_ID"] = "your_google_project_id";
                // context.EnvironmentVariables["GOOGLE_PROJECT_NUMBER"] = "your_google_project_number";
            })
            .WithHttpEndpoint(targetPort: 4000, port: port, name: endpointName);
    }

    /// <summary>
    /// Adds the Edge Runtime module to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithEdgeRuntimeService(
        this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("edge-runtime", SupabaseContainerImageTags.EdgeRuntimeImage,
                SupabaseContainerImageTags.EdgeRuntimeTag)
            .WithEnvironment(context =>
            {
                SupabaseResource resource = builder.Resource;

                // Authentication
                context.EnvironmentVariables["JWT_SECRET"] = resource.DashboardPasswordParameter;

                // Supabase connections
                context.EnvironmentVariables["SUPABASE_URL"] = "http://kong:8000";
                context.EnvironmentVariables["SUPABASE_ANON_KEY"] = resource.DashboardPasswordParameter;
                context.EnvironmentVariables["SUPABASE_SERVICE_ROLE_KEY"] = resource.DashboardPasswordParameter;
                context.EnvironmentVariables["SUPABASE_DB_URL"] =
                    $"postgresql://postgres:{resource.DashboardPasswordParameter}@{resource.DatabaseEndpoint.Property(EndpointProperty.Host)}:{resource.DatabaseEndpoint.Property(EndpointProperty.Port)}/postgres";

                // Security configuration
                context.EnvironmentVariables["FUNCTIONS_VERIFY_JWT"] = "true";
            })
            .WithBindMount("./volumes/functions", "/home/deno/functions")
            .WithArgs("start", "--main-service", "/home/deno/functions/main")
            .WithHttpEndpoint(targetPort: 8000, port: null, name: $"{builder.Resource.Name}-edge-runtime");
    }

    #endregion

    #region Configuration

    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    public static IResourceBuilder<SupabaseResource> WithDashboardUserName(
        this IResourceBuilder<SupabaseResource> builder, IResourceBuilder<ParameterResource> userName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(userName);

        builder.Resource.DashboardUserNameParameter = userName.Resource;
        return builder;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static IResourceBuilder<SupabaseResource> WithDashboardPassword(
        this IResourceBuilder<SupabaseResource> builder,
        IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(password);

        builder.Resource.DashboardPasswordParameter = password.Resource;
        return builder;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="userName"></param>
    /// <returns></returns>
    public static IResourceBuilder<SupabaseResource> WithDatabaseUserName(
        this IResourceBuilder<SupabaseResource> builder,
        IResourceBuilder<ParameterResource> userName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(userName);

        builder.Resource.DatabaseUserNameParameter = userName.Resource;
        return builder;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public static IResourceBuilder<SupabaseResource> WithDatabasePassword(
        this IResourceBuilder<SupabaseResource> builder,
        IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(password);

        builder.Resource.DatabasePasswordParameter = password.Resource;
        return builder;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="script"></param>
    /// <returns></returns>
    public static IResourceBuilder<SupabaseResource> WithCreationScript(this IResourceBuilder<SupabaseResource> builder,
        string script)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(script);

        builder.WithAnnotation(new SupabaseCreationScriptAnnotation(script));

        return builder;
    }


    /// <summary>
    /// Adds a named volume for the data folder to a Supabase container resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <param name="name">Optional name for the volume.</param>
    /// <param name="isReadOnly"></param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithDataVolume(
        this IResourceBuilder<SupabaseResource> builder,
        string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/postgresql/data");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Supabase container resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <param name="source">The source path on the host machine.</param>
    /// <param name="isReadOnly"></param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithDataBindMount(this IResourceBuilder<SupabaseResource> builder,
        string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/var/lib/postgresql/data", isReadOnly);
    }

    /* TODO: fix the WithContainerFiles
                 .WithBindMount("./volumes/db/migrations", "/docker-entrypoint-initdb.d/migrations")
       .WithBindMount("./volumes/db/init-scripts", "/docker-entrypoint-initdb.d/init-scripts")
    /// <summary>
    /// Copies init files to a PostgreSQL container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory or files on the host to copy into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<SupabaseResource> WithInitFiles(this IResourceBuilder<SupabaseResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        const string initPath = "/docker-entrypoint-initdb.d";

        var importFullPath = Path.GetFullPath(source, builder.ApplicationBuilder.AppHostDirectory);

        return builder.WithContainerFiles(initPath, importFullPath);
    }*/

    #endregion
}
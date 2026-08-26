using Aspire.Hosting.ApplicationModel;
using Zemires.Aspire.Hosting.N8n;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding N8n resources to the application model.
/// </summary>
public static partial class N8nBuilderExtensions
{
    private const int N8nPort = 5678;

    /// <summary>
    /// Adds an n8n container resource to the application model.
    /// The default image is <inheritdoc cref="N8nContainerImageTags.Image"/> and the tag is <inheritdoc cref="N8nContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <param name="encryptionKey">The parameter used to provide the master key for the N8n. If <see langword="null"/> a random master key will be generated.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an N8n container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var N8n = builder.AddN8n("n8n");
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<N8nResource> AddN8n(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? encryptionKey = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var encryptionKeyParameter = encryptionKey?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-encryption-key");

        var n8n = new N8nResource(name, encryptionKeyParameter);

        var n8nBuilder = builder.AddResource(n8n)
            .WithAnnotation(new ContainerImageAnnotation { Image = N8nContainerImageTags.Image, Tag = N8nContainerImageTags.Tag, Registry = N8nContainerImageTags.Registry })
            .WithHttpEndpoint(targetPort: N8nPort, port: port, name: N8nResource.PrimaryEndpointName, env: "N8N_PORT")
            .WithHttpHealthCheck("/healthz", 200, N8nResource.PrimaryEndpointName)
            .WithEnvironment("QUEUE_HEALTH_CHECK_ACTIVE", "true")
            .WithIconName("BranchFork", IconVariant.Regular)
            .WithEnvironment("OFFLOAD_MANUAL_EXECUTIONS_TO_WORKERS", "true")
            .WithEnvironment("N8N_ENFORCE_SETTINGS_FILE_PERMISSIONS", "false")
            .WithEnvironment("N8N_ENCRYPTION_KEY", encryptionKeyParameter)
            .WithEnvironment("N8N_WEBHOOK_URL", n8n.GetEndpoint(N8nResource.PrimaryEndpointName, builder.ExecutionContext.IsPublishMode ? KnownNetworkIdentifiers.PublicInternet : KnownNetworkIdentifiers.LocalhostNetwork))
            .WithEnvironment("N8N_PROXY_HOPS", "1");

#pragma warning disable ASPIRECERTIFICATES001
        n8nBuilder.WithHttpsCertificateConfiguration(ctx =>
        {
            ctx.EnvironmentVariables["N8N_PROTOCOL"] = "https";
            ctx.EnvironmentVariables["N8N_SSL_KEY"] = ctx.KeyPath;
            ctx.EnvironmentVariables["N8N_SSL_CERT"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = ctx.CertificatePath;
            return Task.CompletedTask;
        });
#pragma warning restore ASPIRECERTIFICATES001

        if (builder.ExecutionContext.IsRunMode)
        {
#pragma warning disable ASPIRECERTIFICATES001
            builder.Eventing.Subscribe<BeforeStartEvent>((@event, cancellationToken) =>
            {
                var developerCertificateService = @event.Services.GetRequiredService<IDeveloperCertificateService>();

                bool addHttps = false;
                if (!n8nBuilder.Resource.TryGetLastAnnotation<HttpsCertificateAnnotation>(out var annotation))
                {
                    if (developerCertificateService.UseForHttps)
                    {
                        addHttps = true;
                    }
                }
                else if (annotation.UseDeveloperCertificate.GetValueOrDefault(developerCertificateService.UseForHttps) || annotation.Certificate is not null)
                {
                    addHttps = true;
                }

                if (addHttps)
                {
                    // If a TLS certificate is configured, override the endpoint to use HTTPS instead of HTTP
                    n8nBuilder.WithEndpoint(N8nResource.PrimaryEndpointName, ep => ep.UriScheme = "https");
                }

                return Task.CompletedTask;
            });
#pragma warning restore ASPIRECERTIFICATES001
        }

        return n8nBuilder;
    }

    /// <summary>
    /// Adds a named volume for the data folder to a N8n container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an N8n container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var N8n = builder.AddN8n("N8n")
    /// .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(N8n);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithDataVolume(this IResourceBuilder<N8nResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/home/node/.n8n");
    }

    /// <summary>
    /// Configures the N8n resource to use a PostgreSQL database.
    /// This method reads connection properties from the provided <paramref name="database"/>
    /// (which must implement <see cref="IResourceWithConnectionString"/>) and sets the
    /// environment variables required by the n8n image to connect to a Postgres backend.
    /// It also creates a reference relationship and waits for the database resource.
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="database">A resource builder for the PostgreSQL database. Must expose connection string information.</param>
    /// <param name="useTls">Use of TLS for connection.</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided <paramref name="database"/> does not expose connection string information.</exception>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithPostgresDatabase(this IResourceBuilder<N8nResource> builder, IResourceBuilder<IResource> database, bool useTls = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        if (database.Resource is IResourceWithConnectionString resourceWithConnection)
        {
            builder.WithEnvironment("DB_TYPE", "postgresdb")
                .WithEnvironment("DB_POSTGRESDB_DATABASE", $"{resourceWithConnection.GetConnectionProperty("DatabaseName")}")
                .WithEnvironment("DB_POSTGRESDB_HOST", $"{resourceWithConnection.GetConnectionProperty("Host")}")
                .WithEnvironment("DB_POSTGRESDB_PORT", $"{resourceWithConnection.GetConnectionProperty("Port")}")
                .WithEnvironment("DB_POSTGRESDB_USER", $"{resourceWithConnection.GetConnectionProperty("Username")}")
                .WithEnvironment("DB_POSTGRESDB_PASSWORD", $"{resourceWithConnection.GetConnectionProperty("Password")}")
                .WithReferenceRelationship(database)
                .WaitFor(database);

            if (useTls)
            {
                builder.WithEnvironment("DB_POSTGRESDB_SSL_ENABLED", "true")
                    .WithEnvironment("DB_POSTGRESDB_SSL_REJECT_UNAUTHORIZED", "true");
            }

            return builder;
        }
        else
        {
            throw new ArgumentException($"The provided resource '{database.Resource.Name}' does not contain connection string information and cannot be used as a database for N8n.", nameof(database));
        }
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a n8n container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an N8n container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var n8n = builder.AddN8n("n8n")
    /// .WithDataBindMount("./data/N8n/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(n8n);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithDataBindMount(this IResourceBuilder<N8nResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/home/node/.n8n");
    }

    /// <summary>
    /// Sets the timezone for the N8n container by configuring the standard
    /// environment variables used by the image (GENERIC_TIMEZONE and TZ).
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="timeZone">The timezone identifier (for example "UTC" or "America/Los_Angeles").</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithTimeZone(this IResourceBuilder<N8nResource> builder, string timeZone)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithEnvironment("GENERIC_TIMEZONE", timeZone)
            .WithEnvironment("TZ", timeZone);
    }

    /// <summary>
    /// Configures the n8n instance owner via environment variables.
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="email">The instance owner email address (<c>N8N_INSTANCE_OWNER_EMAIL</c>).</param>
    /// <param name="firstName">The instance owner first name (<c>N8N_INSTANCE_OWNER_FIRST_NAME</c>).</param>
    /// <param name="lastName">The instance owner last name (<c>N8N_INSTANCE_OWNER_LAST_NAME</c>).</param>
    /// <param name="password">
    /// The parameter used to provide the plaintext password. The password is bcrypt-hashed before being passed
    /// to the container as <c>N8N_INSTANCE_OWNER_PASSWORD_HASH</c>.
    /// If <see langword="null"/> a random password will be generated.
    /// </param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithInstanceOwner(
        this IResourceBuilder<N8nResource> builder,
        string email,
        string firstName,
        string lastName,
        IResourceBuilder<ParameterResource>? password = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(email);

        var passwordParameter = password?.Resource
            ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder.ApplicationBuilder, $"{builder.Resource.Name}-instance-owner-password");

        builder.Resource.InstanceOwnerPassword = passwordParameter;

        return builder
            .WithEnvironment("N8N_INSTANCE_OWNER_MANAGED_BY_ENV", "true")
            .WithEnvironment("N8N_INSTANCE_OWNER_EMAIL", email)
            .WithEnvironment("N8N_INSTANCE_OWNER_FIRST_NAME", firstName)
            .WithEnvironment("N8N_INSTANCE_OWNER_LAST_NAME", lastName)
            .WithEnvironment("N8N_INSTANCE_OWNER_PASSWORD", passwordParameter) // not used but to store value in aspire json file
            .WithEnvironment(async ctx =>
            {
                var plainPassword = await passwordParameter.GetValueAsync(ctx.CancellationToken);
                ctx.EnvironmentVariables["N8N_INSTANCE_OWNER_PASSWORD_HASH"] = BCrypt.Net.BCrypt.HashPassword(plainPassword);
            });
    }

    /// <summary>
    /// Configures the N8n resource with an enterprise license key.
    /// Sets <c>N8N_LICENSE_ACTIVATION_KEY</c> and suppresses the usage page via <c>N8N_HIDE_USAGE_PAGE</c>.
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="licenseKey">The parameter that contains the license activation key.</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    [AspireExport("withLicenseKeyParameter")]
    public static IResourceBuilder<N8nResource> WithLicenseKey(
        this IResourceBuilder<N8nResource> builder,
        IResourceBuilder<ParameterResource> licenseKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(licenseKey);

        return builder
            .WithEnvironment("N8N_LICENSE_ACTIVATION_KEY", licenseKey.Resource)
            .WithEnvironment("N8N_HIDE_USAGE_PAGE", "true");
    }

    /// <summary>
    /// Adds a license key parameter to the resource's application builder.
    /// </summary>
    /// <remarks>Registers a parameter named "{resourceName}-license-key" (using the resource's Name) via
    /// ApplicationBuilder.AddParameter.</remarks>
    /// <param name="builder">The resource builder to configure.</param>
    /// <param name="licenseKey">The license key value to register as a parameter.</param>
    /// <returns>The same resource builder instance for chaining.</returns>
    [AspireExport("withLicenseKeyString")]
    public static IResourceBuilder<N8nResource> WithLicenseKey(
        this IResourceBuilder<N8nResource> builder,
        string licenseKey) 
            => WithLicenseKey(builder, builder.ApplicationBuilder.AddParameter(builder.Resource.Name + "-license-key", value: licenseKey, secret: true));

    /// <summary>
    /// Adds a license key parameter to the resource's application builder.
    /// </summary>
    /// <remarks>Registers a parameter named "{resourceName}-license-key" (using the resource's Name) via
    /// ApplicationBuilder.AddParameter.</remarks>
    /// <param name="builder">The resource builder to configure.</param>
    /// <returns>The same resource builder instance for chaining.</returns>
    [AspireExport()]
    public static IResourceBuilder<N8nResource> WithLicenseKey(
        this IResourceBuilder<N8nResource> builder)
            => WithLicenseKey(builder, builder.ApplicationBuilder.AddParameter(builder.Resource.Name + "-license-key", secret: true));

    /// <summary>
    /// Adds an OTLP exporter using HTTP/protobuf and maps n8n-specific environment variables to standard OpenTelemetry
    /// environment variables.
    /// </summary>
    /// <remarks>Sets N8N_OTEL_ENABLED to "true" and maps N8N_OTEL_EXPORTER_OTLP_ENDPOINT,
    /// N8N_OTEL_EXPORTER_OTLP_HEADERS, and N8N_OTEL_EXPORTER_SERVICE_NAME to the corresponding
    /// OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS, and OTEL_SERVICE_NAME environment variables.</remarks>
    /// <param name="builder">The resource builder to configure.</param>
    /// <returns>The configured resource builder for chaining.</returns>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithOtlpExporter(this IResourceBuilder<N8nResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithOtlpExporter(OtlpProtocol.HttpProtobuf)
            .WithEnvironment(ctx =>
            {
                if (ctx.EnvironmentVariables.TryGetValue("OTEL_EXPORTER_OTLP_ENDPOINT", out var otelEndpoint))
                {
                    ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_OTLP_ENDPOINT"] = otelEndpoint;
                    ctx.EnvironmentVariables["N8N_OTEL_ENABLED"] = "true";
                }
                else
                {
                    ctx.Logger.LogWarning("OTEL_EXPORTER_OTLP_ENDPOINT is not set. N8n OTLP exporter will not be enabled.");
                }    
                if (ctx.EnvironmentVariables.TryGetValue("OTEL_EXPORTER_OTLP_HEADERS", out var otelHeaders))
                {
                    ctx.EnvironmentVariables["OTEL_EXPORTER_OTLP_HEADERS"] = otelHeaders;
                }
                if (ctx.EnvironmentVariables.TryGetValue("N8N_OTEL_EXPORTER_SERVICE_NAME", out var otelServiceName))
                {
                    ctx.EnvironmentVariables["N8N_OTEL_EXPORTER_SERVICE_NAME"] = otelServiceName;
                }
            });
    }

    /// <summary>
    /// Enables Community Packages and if packages are given, installs the packages as predefined packages.
    /// </summary>
    /// <param name="builder">The resource builder to configure.</param>
    /// <param name="packageNames">package name, e.g.n8n-nodes-foo or n8n-nodes-foo@version </param>
    /// <returns></returns>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithCommunityPackages(this IResourceBuilder<N8nResource> builder, params string[] packageNames)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(packageNames);

        return builder.WithEnvironment(ctx =>
        {
            ctx.EnvironmentVariables["N8N_COMMUNITY_PACKAGES_ENABLED"] = "true";
            if (packageNames.Length > 0)
            {
                ctx.EnvironmentVariables["N8N_COMMUNITY_PACKAGES_MANAGED_BY_ENV"] = "true";
                ctx.EnvironmentVariables["N8N_COMMUNITY_PACKAGES"] = JsonSerializer.Serialize(packageNames.Select(p => new { name = p }).ToArray(), JsonSerializerOptions.Web);
            }
        });
    }

    /// <summary>
    /// Enables metrics collection for the N8n resource and configures which metrics to include.
    /// </summary>
    /// <param name="builder">The N8n resource builder to configure.</param>
    /// <param name="configureOptions">An optional action to configure the metric options. If <see langword="null"/>, all metrics are enabled by default.</param>
    /// <returns>The same <see cref="IResourceBuilder{N8nResource}"/> instance for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables the N8n metrics endpoint by setting <c>N8N_METRICS</c> to <c>true</c> and allows fine-grained control
    /// over which metric categories to include through the <see cref="N8nMetricOptions"/> configuration.
    /// </para>
    /// <example>
    /// Enable all metrics (default behavior):
    /// <code lang="csharp">
    /// var n8n = builder.AddN8n("n8n")
    ///     .WithMetrics();
    /// </code>
    /// </example>
    /// <example>
    /// Enable metrics with custom configuration:
    /// <code lang="csharp">
    /// var n8n = builder.AddN8n("n8n")
    ///     .WithMetrics(options =>
    ///     {
    ///         options.IncludeWebhookMetrics = false;
    ///         options.IncludeQueueMetrics = true;
    ///     });
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<N8nResource> WithMetrics(this IResourceBuilder<N8nResource> builder, Action<N8nMetricOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new N8nMetricOptions();

        if (configureOptions != null)
        {
            configureOptions(options);
        }

        return builder.WithEnvironment("N8N_METRICS", "true")
            .WithEnvironment("N8N_METRICS_INCLUDE_WEBHOOK_METRICS", options.IncludeWebhookMetrics.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_WORKFLOW_INFO", options.IncludeWorkflowInfo.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_FORM_METRICS", options.IncludeFormMetrics.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_WORKFLOW_ID_LABEL", options.IncludeWorkflowIdLabel.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_NODE_TYPE_LABEL", options.IncludeNodeTypeLabel.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_CREDENTIAL_TYPE_LABEL", options.IncludeCredentialTypeLabel.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_API_ENDPOINTS", options.IncludeApiEndpoints.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_API_PATH_LABEL", options.IncludeApiPathLabel.ToString().ToLower())
            .WithEnvironment("N8N_METRICS_INCLUDE_QUEUE_METRICS", options.IncludeQueueMetrics.ToString().ToLower());
    }

    /// <summary>
    /// Configures the external webhook url for the n8n resource. This overrides the default url in <see cref="AddN8n"/>.
    /// </summary>
    /// <ats-summary>Configures the external url for the n8n resource used by webhooks.</ats-summary>
    /// <param name="builder">The n8n resource builder.</param>
    /// <param name="webhookUrl">The external url to use (e.g., "https://n8n.example.com"). Cannot be null or empty.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="webhookUrl"/> is null or whitespace.</exception>
    [AspireExport]
    public static IResourceBuilder<N8nResource> WithWebhookUrl(
        this IResourceBuilder<N8nResource> builder,
        string webhookUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);

        return builder.WithEnvironment("N8N_WEBHOOK_URL", webhookUrl);
    }
}

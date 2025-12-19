using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Zitadel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Zitadel to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class ZitadelHostingExtensions
{
    /// <summary>
    /// Adds a Zitadel container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder" /> to add the Zitadel container to.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port used when launching the container. If <c>null</c> a random port will be assigned</param>
    /// <param name="username">An optional parameter to set a username for the admin account, if <c>null</c> will auto generate one.</param>
    /// <param name="password">An optional parameter to set a password for the admin account, if <c>null</c> will auto generate one.</param>
    /// <param name="masterKey">An optional parameter to set the masterkey, if <c>null</c> will auto generate one.</param>
    public static IResourceBuilder<ZitadelResource> AddZitadel(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        IResourceBuilder<ParameterResource>? username = null,
        IResourceBuilder<ParameterResource>? password = null,
        IResourceBuilder<ParameterResource>? masterKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var usernameParameter = username?.Resource ?? new ParameterResource($"{name}-username", _ => "admin", false);
        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", minSpecial: 1);
        var masterKeyParameter = masterKey?.Resource ?? ParameterResourceBuilderExtensions.CreateGeneratedParameter(builder, $"{name}-masterKey", true, new GenerateParameterDefault
        {
            MinLength = 32, // Zitadel requires 32, CreateDefaultPasswordParameter generates 22
            Lower = true,
            Upper = true,
            Numeric = true,
            Special = true,
            MinLower = 1,
            MinUpper = 1,
            MinNumeric = 1,
            MinSpecial = 1
        });

        var resource = new ZitadelResource(name)
        {
            AdminUsernameParameter = usernameParameter,
            AdminPasswordParameter = passwordParameter
        };

        var zitadelBuilder = builder.AddResource(resource)
            .WithImage(ZitadelContainerImageTags.Image)
            .WithImageTag(ZitadelContainerImageTags.Tag)
            .WithImageRegistry(ZitadelContainerImageTags.Registry)
            .WithArgs("start-from-init", "--masterkeyFromEnv")
            .WithHttpEndpoint(
                targetPort: 8080,
                port: port,
                name: ZitadelResource.HttpEndpointName
            )
            .WithHttpHealthCheck("/healthz")
            .WithEnvironment("ZITADEL_MASTERKEY", masterKeyParameter)
            .WithEnvironment("ZITADEL_TLS_ENABLED", "false")
            .WithEnvironment("ZITADEL_EXTERNALSECURE", "false")
            .WithUrlForEndpoint(ZitadelResource.HttpEndpointName, e => e.DisplayText = "Zitadel Dashboard");

#pragma warning disable ASPIRECERTIFICATES001 Allow for Zitadel SSL support
        zitadelBuilder.WithHttpsCertificateConfiguration(ctx =>
        {
            ctx.EnvironmentVariables["ZITADEL_EXTERNALSECURE"] = "true";
            ctx.EnvironmentVariables["ZITADEL_TLS_ENABLED"] = "true";
            ctx.EnvironmentVariables["ZITADEL_TLS_CERTPATH"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["ZITADEL_TLS_KEYPATH"] = ctx.KeyPath;
            return Task.CompletedTask;
        });
#pragma warning restore ASPIRECERTIFICATES001

        // Use ReferenceExpression for the port to avoid issues with endpoint allocation
        var endpoint = resource.GetEndpoint(ZitadelResource.HttpEndpointName, KnownNetworkIdentifiers.LocalhostNetwork);
        var portExpression = ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.Port)}");
        var hostExpression = ReferenceExpression.Create($"{endpoint.Property(EndpointProperty.Host)}");

        if (builder.ExecutionContext.IsRunMode)
        {
#pragma warning disable ASPIRECERTIFICATES001 Allow for Zitadel SSL support
            builder.Eventing.Subscribe<BeforeStartEvent>((@event, cancellationToken) =>
            {
                var developerCertificateService = @event.Services.GetRequiredService<IDeveloperCertificateService>();

                bool addHttps = false;
                if (!zitadelBuilder.Resource.TryGetLastAnnotation<HttpsCertificateAnnotation>(out var annotation))
                {
                    if (developerCertificateService.UseForHttps)
                    {
                        // If no certificate is configured, and the developer certificate service supports container trust,
                        // configure the resource to use the developer certificate for its key pair.
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
                    // Zitadel only binds to a single port
                    zitadelBuilder
                        .WithEndpoint(ZitadelResource.HttpEndpointName, ep => ep.UriScheme = "https");
                }

                return Task.CompletedTask;
            });
#pragma warning restore ASPIRECERTIFICATES001
        }

        return zitadelBuilder
            .WithEnvironment("ZITADEL_EXTERNALDOMAIN", hostExpression)
            .WithEnvironment("ZITADEL_EXTERNALPORT", portExpression)
            // Disable Login V2 for simpler setup (no separate login container needed)
            .WithEnvironment("ZITADEL_DEFAULTINSTANCE_FEATURES_LOGINV2_REQUIRED", "false")
            // Configure admin user
            .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME", usernameParameter)
            .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORD", passwordParameter)
            .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_PASSWORDCHANGEREQUIRED", "false");
    }

    /// <summary>
    /// Adds database support to the Zitadel resource.
    /// </summary>
    /// <param name="builder">The Zitadel resource to add database support to.</param>
    /// <param name="server">The Postgres server resource to use for the database.</param>
    /// <param name="databaseName">An optional name for the database Zitadel will use, if left empty will default to <c>"zitadel-db"</c>.</param>
    public static IResourceBuilder<ZitadelResource> WithDatabase(
        this IResourceBuilder<ZitadelResource> builder,
        IResourceBuilder<PostgresServerResource> server,
        [ResourceName] string? databaseName = null
    )
    {
        databaseName = string.IsNullOrWhiteSpace(databaseName) ? "zitadel-db" : databaseName;
        var database = server.AddDatabase(databaseName);

        return WithDatabase(builder, database);
    }

    /// <summary>
    /// Adds database support to the Zitadel resource.
    /// </summary>
    /// <param name="builder">The Zitadel resource to add database support to.</param>
    /// <param name="database">The Postgres database resource to use for the database.</param>
    public static IResourceBuilder<ZitadelResource> WithDatabase(this IResourceBuilder<ZitadelResource> builder, IResourceBuilder<PostgresDatabaseResource> database)
    {
        ArgumentNullException.ThrowIfNull(database);

        builder
            .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_USERNAME", database.Resource.Parent.UserNameReference)
            .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_PASSWORD", database.Resource.Parent.PasswordParameter)
            .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_USERNAME", database.Resource.Parent.UserNameReference)
            .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD", database.Resource.Parent.PasswordParameter)
            .WithEnvironment("ZITADEL_DATABASE_POSTGRES_HOST", database.Resource.Parent.Host)
            .WithEnvironment("ZITADEL_DATABASE_POSTGRES_PORT", database.Resource.Parent.Port)
            .WithEnvironment("ZITADEL_DATABASE_POSTGRES_DATABASE", database.Resource.DatabaseName)
            .WithReference(database)
            .WaitFor(database);

        return builder;
    }

    /// <summary>
    /// Configures the external domain for the Zitadel resource. This overrides the default domain set in <see cref="AddZitadel"/>.
    /// </summary>
    /// <param name="builder">The Zitadel resource builder.</param>
    /// <param name="externalDomain">The external domain to use (e.g., "auth.example.com"). Cannot be null or empty.</param>
    /// <returns>The resource builder for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="externalDomain"/> is null or whitespace.</exception>
    public static IResourceBuilder<ZitadelResource> WithExternalDomain(
        this IResourceBuilder<ZitadelResource> builder,
        string externalDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalDomain);

        return builder.WithEnvironment("ZITADEL_EXTERNALDOMAIN", externalDomain);
    }
}
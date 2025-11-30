using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Zitadel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Zitadel to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class ZitadelHostingExtensions
{
    /// <summary>
    /// Adds a Zitadel container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <param name="port"></param>
    /// <param name="password"></param>
    /// <param name="masterKey"></param>
    /// <returns></returns>
    public static IResourceBuilder<ZitadelResource> AddZitadel(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        IResourceBuilder<ParameterResource>? password = null,
        IResourceBuilder<ParameterResource>? masterKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

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
            AdminPasswordParameter = passwordParameter
        };

        return builder.AddResource(resource)
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
            .WithEnvironment("ZITADEL_EXTERNALDOMAIN", $"{name}.dev.localhost")
            .WithUrlForEndpoint(ZitadelResource.HttpEndpointName, e => e.DisplayText = "Zitadel Dashboard")
            .WithEnvironment(static ctx =>
            {
                if (ctx.Resource is ZitadelResource resource)
                {
                    ctx.EnvironmentVariables["ZITADEL_EXTERNALPORT"] =
                        resource.GetEndpoint(ZitadelResource.HttpEndpointName).Port;
                }
            })
            // Disable Login V2 for simpler setup (no separate login container needed)
            .WithEnvironment("ZITADEL_DEFAULTINSTANCE_FEATURES_LOGINV2_REQUIRED", "false")
            // Configure admin user
            .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_USERNAME", "admin")
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
}
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.RavenDB;
using Microsoft.Extensions.DependencyInjection;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using System.Data.Common;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding RavenDB resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class RavenDBBuilderExtensions
{
    /// <summary>
    /// Adds a RavenDB server resource to the application model. A container is used for local development.
    /// This overload simplifies the configuration by creating an unsecured RavenDB server resource with default settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Note:</strong> When using this method, a valid RavenDB license must be provided as an environment variable
    /// before calling the <see cref="IDistributedApplicationBuilder.Build"/> and <see cref="DistributedApplication.Run"/> methods.
    /// You can set the license by calling:
    /// <code>
    /// builder.WithEnvironment("RAVEN_License", "{your license}");
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the resource is added.</param>
    /// <param name="name">The name of the RavenDB server resource.</param>
    /// <returns>A resource builder for the newly added RavenDB server resource.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="builder"/> is null.</exception>
    public static IResourceBuilder<RavenDBServerResource> AddRavenDB(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddRavenDB(name, RavenDBServerSettings.Unsecured());
    }

    /// <summary>
    /// Adds a RavenDB server resource to the application model. A container is used for local development.
    /// This version of the package defaults to the <inheritdoc cref="RavenDBContainerImageTags.Tag"/> tag of the <inheritdoc cref="RavenDBContainerImageTags.Image"/> container image.
    /// This overload simplifies configuration by accepting a <see cref="RavenDBServerSettings"/> object to specify server settings.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/></param>
    /// <param name="name">The name of the RavenDB server resource.</param>
    /// <param name="serverSettings">An object of type <see cref="RavenDBServerSettings"/> containing configuration details for the RavenDB server, 
    /// such as whether the server should use HTTPS, RavenDB license and other relevant settings.</param>
    /// <returns>A resource builder for the newly added RavenDB server resource.</returns>
    /// <exception cref="DistributedApplicationException">Thrown when the connection string cannot be retrieved during configuration.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is unavailable.</exception>
    public static IResourceBuilder<RavenDBServerResource> AddRavenDB(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        RavenDBServerSettings serverSettings)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var environmentVariables = GetEnvironmentVariablesFromServerSettings(serverSettings);
        var securedSettings = serverSettings as RavenDBSecuredServerSettings;

        var serverResource = new RavenDBServerResource(name, isSecured: securedSettings is not null)
        {
            PublicServerUrl = securedSettings?.PublicServerUrl,
            ClientCertificate = securedSettings?.ClientCertificate
        };

        return AddRavenDbInternal(builder, name, serverResource, environmentVariables, serverSettings.Port, serverSettings.TcpPort);
    }

    /// <summary>
    /// Adds a RavenDB server resource to the application model. A container is used for local development.
    /// This version of the package defaults to the <inheritdoc cref="RavenDBContainerImageTags.Tag"/> tag of the <inheritdoc cref="RavenDBContainerImageTags.Image"/> container image.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/></param>
    /// <param name="name">The name of the RavenDB server resource.</param>
    /// <param name="secured">Indicates whether the server connection should be secured (HTTPS). Defaults to false.</param>
    /// <param name="environmentVariables">The environment variables to configure the RavenDB server.</param>
    /// <param name="port">Optional port for the server. If not provided, defaults to the container's internal port (8080).</param>
    /// <returns>A resource builder for the newly added RavenDB server resource.</returns>
    /// <exception cref="DistributedApplicationException">Thrown when the connection string cannot be retrieved during configuration.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is unavailable.</exception>
    public static IResourceBuilder<RavenDBServerResource> AddRavenDB(this IDistributedApplicationBuilder builder,
    [ResourceName] string name,
    bool secured,
    Dictionary<string, object> environmentVariables,
    int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var serverResource = new RavenDBServerResource(name, secured);

        return AddRavenDbInternal(builder, name, serverResource, environmentVariables, port, tcpPort: null);
    }

    private static IResourceBuilder<RavenDBServerResource> AddRavenDbInternal(
    IDistributedApplicationBuilder builder,
    string name,
    RavenDBServerResource serverResource,
    Dictionary<string, object> environmentVariables,
    int? port,
    int? tcpPort)
    {
        string? connectionString = null;
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(serverResource, async (_, ct) =>
        {
            connectionString = await serverResource.ConnectionStringExpression.GetValueAsync(ct)
                .ConfigureAwait(false);

            if (connectionString is null)
                throw new DistributedApplicationException(
                    $"ConnectionStringAvailableEvent was published for the '{serverResource.Name}' resource but the connection string was null.");
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .AddRavenDB(_ => connectionString ?? throw new InvalidOperationException("Connection string is unavailable"),
                name: healthCheckKey,
                certificate: serverResource.ClientCertificate);

        var effectiveTcpPort = tcpPort ?? 38888;
        
        return builder.AddResource(serverResource)
            .WithEndpoint(
                port: port,
                targetPort: serverResource.IsSecured ? 443 : 8080,
                scheme: serverResource.PrimaryEndpointName,
                name: serverResource.PrimaryEndpointName,
                isProxied: false)
            .WithEndpoint(
                port: effectiveTcpPort,
                targetPort: effectiveTcpPort,
                name: serverResource.TcpEndpointName,
                isProxied: false)
            .WithImage(RavenDBContainerImageTags.Image, RavenDBContainerImageTags.Tag)
            .WithImageRegistry(RavenDBContainerImageTags.Registry)
            .WithEnvironment(context => ConfigureEnvironmentVariables(context, serverResource, environmentVariables))
            .WithHealthCheck(healthCheckKey);
    }

    private static Dictionary<string, object> GetEnvironmentVariablesFromServerSettings(RavenDBServerSettings serverSettings)
    {
        var environmentVariables = new Dictionary<string, object>
        {
            { "RAVEN_Setup_Mode", serverSettings.SetupMode.ToString() }
        };

        if (serverSettings.LicensingOptions is not null)
        {
            environmentVariables.TryAdd("RAVEN_License_Eula_Accepted", serverSettings.LicensingOptions.EulaAccepted.ToString());
            environmentVariables.TryAdd("RAVEN_License", serverSettings.LicensingOptions.License);
        }

        if (serverSettings.ServerUrl is not null)
            environmentVariables.TryAdd("RAVEN_ServerUrl", serverSettings.ServerUrl);

        if (serverSettings is RavenDBSecuredServerSettings securedServerSettings)
        {
            environmentVariables.TryAdd("RAVEN_PublicServerUrl", securedServerSettings.PublicServerUrl);
            environmentVariables.TryAdd("RAVEN_Security_Certificate_Path", securedServerSettings.CertificatePath);

            if (securedServerSettings.CertificatePassword is not null)
                environmentVariables.TryAdd("RAVEN_Security_Certificate_Password", securedServerSettings.CertificatePassword);

            var publicUri = new Uri(securedServerSettings.PublicServerUrl);
            environmentVariables.TryAdd("RAVEN_PublicServerUrl_Tcp", $"tcp://{publicUri.Host}:{serverSettings.TcpPort ?? 38888}");
        }

        return environmentVariables;
    }

    private static void ConfigureEnvironmentVariables(EnvironmentCallbackContext context, RavenDBServerResource serverResource, Dictionary<string, object>? environmentVariables = null)
    {
        context.EnvironmentVariables.TryAdd("RAVEN_ServerUrl_Tcp", $"{serverResource.TcpEndpoint.Scheme}://0.0.0.0:{serverResource.TcpEndpoint.Port}");

        if (environmentVariables is null)
        {
            context.EnvironmentVariables.TryAdd("RAVEN_Setup_Mode", "None");
            context.EnvironmentVariables.TryAdd("RAVEN_Security_UnsecuredAccessAllowed", "PrivateNetwork");
        }
        else
        {
            foreach (var environmentVariable in environmentVariables)
                context.EnvironmentVariables.TryAdd(environmentVariable.Key, environmentVariable.Value);
        }

        context.EnvironmentVariables.TryAdd("RAVEN_PublicServerUrl_Tcp", serverResource.TcpEndpoint.Url);
    }

    /// <summary>
    /// Adds a database resource to an existing RavenDB server resource.
    /// </summary>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="name">The name of the database resource.</param>
    /// <param name="databaseName">The name of the database to create/add. Defaults to the same name as the resource if not provided.</param>
    /// <param name="ensureCreated">Indicates whether the database should be created on startup if it does not already exist.</param>
    /// <returns>A resource builder for the newly added RavenDB database resource.</returns>
    /// <exception cref="DistributedApplicationException">Thrown when the connection string cannot be retrieved during configuration.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is unavailable.</exception>
    public static IResourceBuilder<RavenDBDatabaseResource> AddDatabase(this IResourceBuilder<RavenDBServerResource> builder,
        [ResourceName] string name,
        string? databaseName = null,
        bool ensureCreated = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        // Use the resource name as the database name if it's not provided
        databaseName ??= name;

        builder.Resource.AddDatabase(name, databaseName);
        var databaseResource = new RavenDBDatabaseResource(name, databaseName, builder.Resource);

        string? connectionString = null;

        builder.ApplicationBuilder.Eventing.Subscribe<ConnectionStringAvailableEvent>(databaseResource, async (@event, ct) =>
        {
            connectionString = await databaseResource.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);

            if (connectionString is null)
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{databaseResource.Name}' resource but the connection string was null.");
        });

        var healthCheckKey = $"{name}_check";
        builder.ApplicationBuilder.Services.AddHealthChecks()
            .AddRavenDB(sp => connectionString ?? throw new InvalidOperationException("Connection string is unavailable"),
                databaseName: databaseName,
                name: healthCheckKey, 
                certificate: databaseResource.Parent.ClientCertificate);

        var dbBuilder = builder.ApplicationBuilder.AddResource(databaseResource);

        if (ensureCreated)
        {
            dbBuilder.OnResourceReady(async (resource, _, ct) =>
            {
                var connString = await databaseResource.ConnectionStringExpression.GetValueAsync(ct);
                if (string.IsNullOrEmpty(connString))
                    throw new InvalidOperationException("RavenDB connection string is not available.");

                var csb = new DbConnectionStringBuilder { ConnectionString = connString };

                if (!csb.TryGetValue("URL", out var urlObj) || urlObj is not string url)
                    throw new InvalidOperationException("Connection string is missing 'URL'.");

                using var store = new DocumentStore
                {
                    Urls = [url],
                    Certificate = databaseResource.Parent.ClientCertificate
                }.Initialize();

                var record = await store.Maintenance.Server
                    .SendAsync(new GetDatabaseRecordOperation(resource.DatabaseName), ct);

                if (record == null)
                    await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(resource.DatabaseName)), ct);

            });
        }

        return dbBuilder;
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a RavenDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">Indicates whether the bind mount should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
    public static IResourceBuilder<RavenDBServerResource> WithDataBindMount(this IResourceBuilder<RavenDBServerResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/lib/ravendb/data", isReadOnly);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a RavenDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="name">Optional name for the volume. Defaults to a generated name if not provided.</param>
    /// <param name="isReadOnly">Indicates whether the volume should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
    public static IResourceBuilder<RavenDBServerResource> WithDataVolume(this IResourceBuilder<RavenDBServerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/ravendb/data", isReadOnly);
    }

    /// <summary>
    /// Adds a bind mount for the logs folder to a RavenDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">Indicates whether the bind mount should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
    public static IResourceBuilder<RavenDBServerResource> WithLogBindMount(
        this IResourceBuilder<RavenDBServerResource> builder,
        string source,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/log/ravendb/logs", isReadOnly);
    }

    /// <summary>
    /// Adds a named volume for the logs folder to a RavenDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder for the RavenDB server.</param>
    /// <param name="name">
    /// Optional name for the volume. Defaults to a generated name if not provided.
    /// </param>
    /// <param name="isReadOnly">Indicates whether the volume should be read-only. Defaults to false.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
    public static IResourceBuilder<RavenDBServerResource> WithLogVolume(
        this IResourceBuilder<RavenDBServerResource> builder,
        string? name = null,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "logs"), "/var/log/ravendb/logs", isReadOnly);
    }

}

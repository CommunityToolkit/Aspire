using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding listmonk resources to the application model.
/// </summary>
public static class ListmonkBuilderExtensions
{
    private const int ListmonkPort = 9000;
    private const string UploadsPath = "/listmonk/uploads";
    private const string AppAddressEnvVarName = "LISTMONK_app__address";
    private const string DatabaseHostEnvVarName = "LISTMONK_db__host";
    private const string DatabasePortEnvVarName = "LISTMONK_db__port";
    private const string DatabaseUserEnvVarName = "LISTMONK_db__user";
    private const string DatabasePasswordEnvVarName = "LISTMONK_db__password";
    private const string DatabaseNameEnvVarName = "LISTMONK_db__database";
    private const string DatabaseSslModeEnvVarName = "LISTMONK_db__ssl_mode";
    private const string DatabaseMaxOpenEnvVarName = "LISTMONK_db__max_open";
    private const string DatabaseMaxIdleEnvVarName = "LISTMONK_db__max_idle";
    private const string DatabaseMaxLifetimeEnvVarName = "LISTMONK_db__max_lifetime";
    private const string DatabaseParamsEnvVarName = "LISTMONK_db__params";
    private const string TimeZoneEnvVarName = "TZ";
    private const string AdminUserEnvVarName = "LISTMONK_ADMIN_USER";
    private const string AdminPasswordEnvVarName = "LISTMONK_ADMIN_PASSWORD";
    private const string UserIdEnvVarName = "PUID";
    private const string GroupIdEnvVarName = "PGID";

    /// <summary>
    /// Adds a listmonk container resource to the application model.
    /// The default image is <inheritdoc cref="ListmonkContainerImageTags.Image"/> and the tag is <inheritdoc cref="ListmonkContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for the listmonk HTTP endpoint. If <see langword="null"/>, Aspire will assign a random host port.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a listmonk container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var db = builder.AddPostgres("postgres")
    ///   .AddDatabase("db");
    /// var listmonk = builder.AddListmonk("listmonk")
    ///   .WithReference(db);
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(listmonk);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> AddListmonk(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resource = new ListmonkResource(name);
        return builder.AddResource(resource)
            .WithImage(ListmonkContainerImageTags.Image, ListmonkContainerImageTags.Tag)
            .WithImageRegistry(ListmonkContainerImageTags.Registry)
            .WithHttpEndpoint(port: port, targetPort: ListmonkPort, name: ListmonkResource.PrimaryEndpointName)
            .WithEntrypoint("sh")
            .WithArgs("-c", "./listmonk --install --idempotent --yes --config '' && ./listmonk --upgrade --yes --config '' && ./listmonk --config ''")
            .WithEnvironment(AppAddressEnvVarName, "0.0.0.0:9000")
            .WithHttpHealthCheck("/health");
    }

    /// <summary>
    /// Configures the listmonk web server address.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="address">The address value for <c>LISTMONK_app__address</c>, for example <c>0.0.0.0:9000</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithAppAddress(this IResourceBuilder<ListmonkResource> builder, string address)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(address);

        return builder.WithEnvironment(AppAddressEnvVarName, address);
    }

    /// <summary>
    /// References a <see cref="PostgresDatabaseResource"/> as the PostgreSQL database for the listmonk resource.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="database">The PostgreSQL database resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithReference(
        this IResourceBuilder<ListmonkResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        var postgres = database.Resource.Parent;

        return builder
            .WithEnvironment(DatabaseHostEnvVarName, postgres.Name)
            .WithEnvironment(DatabasePortEnvVarName, ReferenceExpression.Create($"{postgres.PrimaryEndpoint.Property(EndpointProperty.TargetPort)}"))
            .WithEnvironment(DatabaseUserEnvVarName, postgres.UserNameReference)
            .WithEnvironment(DatabasePasswordEnvVarName, postgres.PasswordParameter)
            .WithEnvironment(DatabaseNameEnvVarName, ReferenceExpression.Create($"{database.Resource.DatabaseName}"))
            .WithDatabaseSslMode("disable")
            .WaitFor(database);
    }

    /// <summary>
    /// Configures the PostgreSQL SSL mode used by listmonk.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="sslMode">The PostgreSQL SSL mode value for <c>LISTMONK_db__ssl_mode</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithDatabaseSslMode(this IResourceBuilder<ListmonkResource> builder, string sslMode)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(sslMode);

        return builder.WithEnvironment(DatabaseSslModeEnvVarName, sslMode);
    }

    /// <summary>
    /// Configures the maximum number of open PostgreSQL connections.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="maxOpen">The value for <c>LISTMONK_db__max_open</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithDatabaseMaxOpenConnections(this IResourceBuilder<ListmonkResource> builder, int maxOpen)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(maxOpen);

        return builder.WithEnvironment(DatabaseMaxOpenEnvVarName, maxOpen.ToString());
    }

    /// <summary>
    /// Configures the maximum number of idle PostgreSQL connections.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="maxIdle">The value for <c>LISTMONK_db__max_idle</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithDatabaseMaxIdleConnections(this IResourceBuilder<ListmonkResource> builder, int maxIdle)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(maxIdle);

        return builder.WithEnvironment(DatabaseMaxIdleEnvVarName, maxIdle.ToString());
    }

    /// <summary>
    /// Configures the maximum lifetime for PostgreSQL connections.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="maxLifetime">The duration value for <c>LISTMONK_db__max_lifetime</c>, for example <c>300s</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithDatabaseMaxLifetime(this IResourceBuilder<ListmonkResource> builder, string maxLifetime)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(maxLifetime);

        return builder.WithEnvironment(DatabaseMaxLifetimeEnvVarName, maxLifetime);
    }

    /// <summary>
    /// Configures additional PostgreSQL DSN parameters.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="parameters">The space-separated parameter string for <c>LISTMONK_db__params</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithDatabaseParameters(this IResourceBuilder<ListmonkResource> builder, string parameters)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(parameters);

        return builder.WithEnvironment(DatabaseParamsEnvVarName, parameters);
    }

    /// <summary>
    /// Configures the time zone used by the listmonk container.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="timeZone">The time zone value for <c>TZ</c>, for example <c>Etc/UTC</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithTimeZone(this IResourceBuilder<ListmonkResource> builder, string timeZone)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(timeZone);

        return builder.WithEnvironment(TimeZoneEnvVarName, timeZone);
    }

    /// <summary>
    /// Configures the first-run listmonk Super Admin username.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="username">The value for <c>LISTMONK_ADMIN_USER</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithAdminUser(this IResourceBuilder<ListmonkResource> builder, string username)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(username);

        return builder.WithEnvironment(AdminUserEnvVarName, username);
    }

    /// <summary>
    /// Configures the first-run listmonk Super Admin password.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="password">The parameter resource used for <c>LISTMONK_ADMIN_PASSWORD</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithAdminPassword(this IResourceBuilder<ListmonkResource> builder, IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(password);

        return builder.WithEnvironment(AdminPasswordEnvVarName, password.Resource);
    }

    /// <summary>
    /// Configures the first-run listmonk Super Admin credentials.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="username">The value for <c>LISTMONK_ADMIN_USER</c>.</param>
    /// <param name="password">The parameter resource used for <c>LISTMONK_ADMIN_PASSWORD</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithAdminCredentials(
        this IResourceBuilder<ListmonkResource> builder,
        string username,
        IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentNullException.ThrowIfNull(password);

        return builder
            .WithAdminUser(username)
            .WithAdminPassword(password);
    }

    /// <summary>
    /// Configures the user ID used by the listmonk container entrypoint.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="userId">The value for <c>PUID</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithUserId(this IResourceBuilder<ListmonkResource> builder, int userId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(userId);

        return builder.WithEnvironment(UserIdEnvVarName, userId.ToString());
    }

    /// <summary>
    /// Configures the group ID used by the listmonk container entrypoint.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="groupId">The value for <c>PGID</c>.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithGroupId(this IResourceBuilder<ListmonkResource> builder, int groupId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentOutOfRangeException.ThrowIfNegative(groupId);

        return builder.WithEnvironment(GroupIdEnvVarName, groupId.ToString());
    }

    /// <summary>
    /// Adds a named volume for listmonk media uploads.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithUploadsVolume(this IResourceBuilder<ListmonkResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "uploads"), UploadsPath);
    }

    /// <summary>
    /// Adds a bind mount for listmonk media uploads.
    /// </summary>
    /// <param name="builder">The listmonk resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ListmonkResource> WithUploadsBindMount(this IResourceBuilder<ListmonkResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, UploadsPath);
    }
}

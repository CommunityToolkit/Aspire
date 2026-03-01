using System.Globalization;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Neon database resource.
/// </summary>
public sealed class NeonDatabaseResource : Resource, IResourceWithParent<NeonProjectResource>, IResourceWithConnectionString, IResourceWithWaitSupport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NeonDatabaseResource"/> class.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="databaseName">The database name.</param>
    /// <param name="roleName">The role name.</param>
    /// <param name="parent">The parent project resource.</param>
    public NeonDatabaseResource(
        [ResourceName] string name,
        string databaseName,
        string roleName,
        NeonProjectResource parent
    ) : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(roleName);

        DatabaseName = databaseName;
        RoleName = roleName;
        Parent = parent;
    }

    /// <summary>
    /// Gets the parent Neon project resource.
    /// </summary>
    public NeonProjectResource Parent { get; }

    /// <summary>
    /// Gets the database name.
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// Gets the role name.
    /// </summary>
    public string RoleName { get; }

    /// <summary>
    /// Gets or sets the connection URI for the database.
    /// </summary>
    public string? ConnectionUri { get; internal set; }

    /// <summary>
    /// Gets or sets the host for the compute endpoint.
    /// </summary>
    public string? Host { get; internal set; }

    /// <summary>
    /// Gets or sets the port for the compute endpoint.
    /// </summary>
    public int? Port { get; internal set; }

    /// <summary>
    /// Gets or sets the password for the role.
    /// </summary>
    public string? Password { get; internal set; }

    /// <summary>
    /// Gets the connection string expression for the Neon database.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        CreateLiteralExpression(ConnectionUri ?? string.Empty);

    /// <summary>
    /// Gets the connection string for the Neon database.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>The connection string.</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        new(ConnectionUri);

    IEnumerable<KeyValuePair<string, ReferenceExpression>>
        IResourceWithConnectionString.GetConnectionProperties()
    {
        if (Host is not null)
        {
            yield return new("Host", CreateLiteralExpression(Host));
        }

        if (Port is not null)
        {
            yield return new(
                "Port",
                CreateLiteralExpression(Port.Value.ToString(CultureInfo.InvariantCulture)));
        }

        yield return new("Database", CreateLiteralExpression(DatabaseName));
        yield return new("Username", CreateLiteralExpression(RoleName));

        if (Password is not null)
        {
            yield return new(
                "Password",
                CreateLiteralExpression(Uri.EscapeDataString(Password)));
        }

        if (ConnectionUri is not null)
        {
            yield return new("Uri", CreateLiteralExpression(ConnectionUri));
        }
    }

    private static ReferenceExpression CreateLiteralExpression(string value)
    {
        ReferenceExpressionBuilder builder = new();
        builder.AppendLiteral(value);
        return builder.Build();
    }
}
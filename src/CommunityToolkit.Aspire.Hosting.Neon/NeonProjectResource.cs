using System.Globalization;
using CommunityToolkit.Aspire.Hosting.Neon;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Neon Postgres project resource.
/// </summary>
public sealed class NeonProjectResource : Resource, IResourceWithConnectionString, IResourceWithWaitSupport
{
    private readonly Dictionary<string, NeonDatabaseResource> _databases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="NeonProjectResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="apiKey">The Neon API key parameter.</param>
    public NeonProjectResource(
        [ResourceName] string name,
        ParameterResource apiKey
    ) : base(name)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        ApiKeyParameter = apiKey;
        Options = new NeonProjectOptions();
    }

    /// <summary>
    /// Gets the Neon API key parameter.
    /// </summary>
    public ParameterResource ApiKeyParameter { get; }

    /// <summary>
    /// Gets the Neon project options.
    /// </summary>
    public NeonProjectOptions Options { get; }

    /// <summary>
    /// Gets the project ID resolved from Neon.
    /// </summary>
    public string? ProjectId { get; internal set; }

    /// <summary>
    /// Gets the branch ID resolved from Neon.
    /// </summary>
    public string? BranchId { get; internal set; }

    /// <summary>
    /// Gets the endpoint ID resolved from Neon.
    /// </summary>
    public string? EndpointId { get; internal set; }

    /// <summary>
    /// Gets the host name for the compute endpoint.
    /// </summary>
    public string? Host { get; internal set; }

    /// <summary>
    /// Gets the port for the compute endpoint.
    /// </summary>
    public int? Port { get; internal set; }

    /// <summary>
    /// Gets the database name used for the connection string.
    /// </summary>
    public string? DatabaseName { get; internal set; }

    /// <summary>
    /// Gets the role name used for the connection string.
    /// </summary>
    public string? RoleName { get; internal set; }

    /// <summary>
    /// Gets the password for the role used in the connection string.
    /// </summary>
    public string? Password { get; internal set; }

    /// <summary>
    /// Gets the connection URI for the project.
    /// </summary>
    public string? ConnectionUri { get; internal set; }

    /// <summary>
    /// Gets the provisioner resource associated with this Neon resource.
    /// </summary>
    public IResourceWithWaitSupport? ProvisionerResource { get; internal set; }

    /// <summary>
    /// Gets the connection string expression for the Neon project.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        CreateLiteralExpression(ConnectionUri ?? string.Empty);

    /// <summary>
    /// Gets the connection string for the Neon project.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>The connection string.</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        new(ConnectionUri);

    internal IReadOnlyDictionary<string, NeonDatabaseResource> Databases => _databases;

    internal void AddDatabase(NeonDatabaseResource database)
    {
        _databases.TryAdd(database.Name, database);
    }

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

        if (DatabaseName is not null)
        {
            yield return new("Database", CreateLiteralExpression(DatabaseName));
        }

        if (RoleName is not null)
        {
            yield return new("Username", CreateLiteralExpression(RoleName));
        }

        if (Password is not null)
        {
            yield return new("Password", CreateLiteralExpression(Uri.EscapeDataString(Password)));
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
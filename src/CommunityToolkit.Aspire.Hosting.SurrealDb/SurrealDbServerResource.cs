// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a SurrealDB container.
/// </summary>
public class SurrealDbServerResource : ContainerResource, IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "tcp";

    private const string DefaultUserName = "root";
    private const string SchemeUri = "ws";

    /// <summary>
    /// Initializes a new instance of the <see cref="SurrealDbServerResource"/> class.
    /// </summary>
    /// <param name="name">The name of the resource.</param>
    /// <param name="userName">A parameter that contains the SurrealDB username.</param>
    /// <param name="password">A parameter that contains the SurrealDB password.</param>
    public SurrealDbServerResource(
        [ResourceName] string name,
        ParameterResource? userName,
        ParameterResource password
    ) : base(name)
    {
        ArgumentNullException.ThrowIfNull(password);

        PrimaryEndpoint = new(this, PrimaryEndpointName);
        UserNameParameter = userName;
        PasswordParameter = password;
    }

    /// <summary>
    /// Gets the primary endpoint for the SurrealDB instance.
    /// </summary>
    public EndpointReference PrimaryEndpoint { get; }

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the parameter that contains the SurrealDB username.
    /// </summary>
    public ParameterResource? UserNameParameter { get; }

    internal ReferenceExpression UserNameReference =>
        UserNameParameter is not null ?
            ReferenceExpression.Create($"{UserNameParameter}") :
            ReferenceExpression.Create($"{DefaultUserName}");

    /// <summary>
    /// Gets the parameter that contains the SurrealDB password.
    /// </summary>
    public ParameterResource PasswordParameter { get; }

    private ReferenceExpression ConnectionString =>
        ReferenceExpression.Create(
            $"Server={SchemeUri}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}/rpc;User={UserNameReference};Password={PasswordParameter}");

    /// <summary>
    /// Gets the connection string expression for the SurrealDB instance.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression
    {
        get
        {
            if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
            {
                return connectionStringAnnotation.Resource.ConnectionStringExpression;
            }

            return ConnectionString;
        }
    }

    /// <summary>
    /// Gets the connection URI expression for the SurrealDB instance.
    /// </summary>
    /// <remarks>
    /// Format: <c>ws://{host}:{port}/rpc</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{SchemeUri}://{Host}:{Port}/rpc");

    /// <summary>
    /// Gets the connection string for the SurrealDB instance.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the SurrealDB instance in the form "Server=scheme://host:port;User=username;Password=password".</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionString.GetValueAsync(cancellationToken);
    }

    private readonly Dictionary<string, string> _namespaces = new(StringComparer.Ordinal);

    /// <summary>
    /// A dictionary where the key is the resource name and the value is the namespace name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Namespaces => _namespaces;

    internal void AddNamespace(string name, string namespaceName)
    {
        _namespaces.TryAdd(name, namespaceName);
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Username", UserNameReference);
        yield return new("Password", ReferenceExpression.Create($"{PasswordParameter:uri}"));
        yield return new("Uri", UriExpression);
    }
}
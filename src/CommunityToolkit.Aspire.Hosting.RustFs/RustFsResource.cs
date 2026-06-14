namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a RustFs S3-compatible storage container.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="accessKey">A parameter that contains the RustFs access key.</param>
/// <param name="secretKey">A parameter that contains the RustFs secret key.</param>
public sealed class RustFsResource(string name, ParameterResource accessKey, ParameterResource secretKey)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "http";
    internal const string ConsoleEndpointName = "console";

    internal const int PrimaryTargetPort = 9000;
    internal const int ConsoleTargetPort = 9001;

    internal const string DefaultSigningRegion = "us-east-1";

    /// <summary>
    /// Gets the access key parameter resource for RustFs.
    /// </summary>
    public ParameterResource AccessKey { get; } = accessKey;

    /// <summary>
    /// Gets the secret key parameter resource for RustFs.
    /// </summary>
    public ParameterResource SecretKey { get; } = secretKey;

    /// <summary>
    /// Gets the AWS signing region used for S3 API requests. Defaults to <c>us-east-1</c>.
    /// </summary>
    public string SigningRegion { get; internal set; } = DefaultSigningRegion;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the RustFs resource. This endpoint is used for all S3-compatible API calls over HTTP.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the connection string expression for the RustFs resource.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => GetConnectionString();

    /// <summary>
    /// Gets the connection URI expression for the RustFs server.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"http://{Host}:{Port}");

    /// <summary>
    /// Gets the connection string for the RustFs server.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the RustFs server in the form "Endpoint=http://host:port;AccessKey=key;SecretKey=secret".</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }

    private ReferenceExpression GetConnectionString()
    {
        var builder = new ReferenceExpressionBuilder();

        builder.Append(
            $"Endpoint=http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

        builder.Append($";AccessKey={AccessKey}");
        builder.Append($";SecretKey={SecretKey}");

        return builder.Build();
    }

    /// <inheritdoc/>
    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("AccessKey", ReferenceExpression.Create($"{AccessKey}"));
        yield return new("SecretKey", ReferenceExpression.Create($"{SecretKey}"));
        yield return new("Uri", UriExpression);
    }
}

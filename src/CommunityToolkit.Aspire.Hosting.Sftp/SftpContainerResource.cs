using System;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an SFTP container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class SftpContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const int SftpEndpointPort = 22;
    internal const string SftpEndpointName = "sftp";
    internal const string SftpEndpointScheme = "sftp";

    private EndpointReference? _sftpEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the SFTP server.
    /// </summary>
    private EndpointReference SftpEndpoint => _sftpEndpoint ??= new(this, SftpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Host => SftpEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for this resource.
    /// </summary>
    public EndpointReferenceExpression Port => SftpEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// ConnectionString for the atmoz SFTP server in the form of sftp://host:port.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{SftpEndpoint.Url}");

    /// <summary>
    /// Gets the connection URI expression for the atmoz SFTP endpoint.
    /// </summary>
    /// <remarks>
    /// Format: <c>sftp://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ConnectionStringExpression;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        if (Uri.TryCreate(SftpEndpoint.Url, UriKind.Absolute, out var uri))
        {
            yield return new("Host", ReferenceExpression.Create($"{uri.Host}"));
            yield return new("Port", ReferenceExpression.Create($"{uri.Port.ToString()}"));
        }

        yield return new("Uri", UriExpression);
    }
}

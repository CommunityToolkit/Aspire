#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the MailPit server.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class MailPitContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const int HttpEndpointPort = 8025;
    internal const int SmtpEndpointPort = 1025;
    internal const string SmtpEndpointName = "smtp";
    internal const string HttpEndpointName = "http";
    internal const string DatabaseEnvVar = "MP_DATABASE";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary SMTP endpoint for the MailPit server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, SmtpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for the SMTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for the SMTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// ConnectionString for MailPit smtp endpoint in the form of smtp://host:port.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"Endpoint={PrimaryEndpoint.Scheme}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");

    /// <summary>
    /// Gets the connection URI expression for the MailPit SMTP endpoint.
    /// </summary>
    /// <remarks>
    /// Format: <c>smtp://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{PrimaryEndpoint.Scheme}://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

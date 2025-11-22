namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Papercut SMTP server.
/// </summary>
/// <param name="name"></param>
public class PapercutSmtpContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const int HttpEndpointPort = 8080;
    internal const int SmtpEndpointPort = 2525;
    internal const string HttpEndpointName = "http";
    internal const string SmtpEndpointName = "smtp";
    private EndpointReference? _smtpEndpoint;
    private EndpointReference SmtpEndpoint => _smtpEndpoint ??= new EndpointReference(this, SmtpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for the SMTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => SmtpEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for the SMTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => SmtpEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// ConnectionString for the Papercut SMTP server in the form of smtp://host:port.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"Endpoint={SmtpEndpoint.Scheme}://{SmtpEndpoint.Property(EndpointProperty.Host)}:{SmtpEndpoint.Property(EndpointProperty.Port)}");

    /// <summary>
    /// Gets the connection URI expression for the Papercut SMTP endpoint.
    /// </summary>
    /// <remarks>
    /// Format: <c>smtp://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{SmtpEndpoint.Scheme}://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

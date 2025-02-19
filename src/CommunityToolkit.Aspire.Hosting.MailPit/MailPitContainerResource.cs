namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the MailPit server.
/// </summary>
/// <param name="name"></param>
public class MailPitContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const int HttpEndpointPort = 8025;
    internal const int SmtpEndpointPort = 1025;
    internal const string SmtpEndpointName = "smtp";
    internal const string HttpEndpointName = "http";
    internal const string DatabaseEnvVar = "MP_DATABASE";

    private EndpointReference? _smtpEndpoint;
    private EndpointReference SmtpEndpoint => _smtpEndpoint ??= new EndpointReference(this, SmtpEndpointName);

    /// <summary>
    /// ConnectionString for MailPit smtp endpoint in the form of smtp://host:port.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"Endpoint={SmtpEndpoint.Scheme}://{SmtpEndpoint.Property(EndpointProperty.Host)}:{SmtpEndpoint.Property(EndpointProperty.Port)}");
}

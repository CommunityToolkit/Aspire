namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Papercut SMTP server.
/// </summary>
/// <param name="name"></param>
public class PapercutSmtpContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const int HttpEndpointPort = 80;
    internal const int SmtpEndpointPort = 25;
    internal const string HttpEndpointName = "http";
    internal const string SmtpEndpointName = "smtp";
    private EndpointReference? _smtpEndpoint;

    /// <summary>
    /// ConnectionString for the Papercut SMTP server in the form of smtp://host:port.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"smtp://{SmtpEndpoint.Property(EndpointProperty.Host)}:{SmtpEndpoint.Property(EndpointProperty.Port)}");

    private EndpointReference SmtpEndpoint => _smtpEndpoint ??= new EndpointReference(this, SmtpEndpointName);
}

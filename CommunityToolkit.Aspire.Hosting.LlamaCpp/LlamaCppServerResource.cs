using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.LlamaCpp;

public class LlamaCppServerResource: ContainerResource, IResourceWithConnectionString
{
    internal const string LlamaServerEndpointName = "http";
    internal string ModelName = string.Empty;
    internal List<string> EnvironmentArgs = [];
    internal string? VolumeName;

    private readonly EndpointReference _primaryEndpoint;

    public LlamaCppServerResource(string name) : base(name)
    {
        _primaryEndpoint = new(this, LlamaServerEndpointName);
    }

    public EndpointReferenceExpression Host => _primaryEndpoint.Property(EndpointProperty.Host);

    public EndpointReferenceExpression Port => _primaryEndpoint.Property(EndpointProperty.Port);

    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"Endpoint={_primaryEndpoint.Property(EndpointProperty.Scheme)}://{_primaryEndpoint.Property(EndpointProperty.Host)}:{_primaryEndpoint.Property(EndpointProperty.Port)}"
     );

    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{_primaryEndpoint.Property(EndpointProperty.Scheme)}://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

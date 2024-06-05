namespace Aspire.Hosting.ApplicationModel
{
  public class OllamaResource : ContainerResource, IResourceWithConnectionString
  {
    public const string OllamaEndpointName = "ollama";

    private EndpointReference? _endpointReference;

    public OllamaResource(string name, string initialModel)
      : base(name)
    {
      InitialModel = initialModel;
    }

    public string InitialModel { get; set; }

    public EndpointReference Endpoint =>
      _endpointReference ??= new EndpointReference(this, OllamaEndpointName);

    public ReferenceExpression ConnectionStringExpression =>
      ReferenceExpression.Create(
        $"http://{Endpoint.Property(EndpointProperty.Host)}:{Endpoint.Property(EndpointProperty.Port)}"
      );
  }
}

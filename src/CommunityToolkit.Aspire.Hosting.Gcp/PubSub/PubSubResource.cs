namespace CommunityToolkit.Aspire.Hosting.Gcp.PubSub;

public class PubSubResource: ContainerResource, IResourceWithConnectionString
{
    internal const int Port = 8085;
    internal const string EndpointName = "http";

    internal List<TopicResource> Topics { get; } = [];

    private EndpointReference? _endpoint;

    public ParameterResource ProjectId { get; }

    public PubSubResource(string name, ParameterResource projectId) : base(name)
    {
        ProjectId = projectId;
    }

    public EndpointReference Endpoint => _endpoint ??= new EndpointReference(this, EndpointName);


    private PublisherServiceApiClient? _client;

    internal async ValueTask<PublisherServiceApiClient> GetClientAsync(CancellationToken ct = default)
    {
        if (_client is not null)
        {
            return _client;
        }

        var builder = new PublisherServiceApiClientBuilder
        {
            Endpoint = Endpoint.Url,
            ChannelCredentials = ChannelCredentials.Insecure
        };
        _client = await builder.BuildAsync(ct);
        return _client;
    }

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Endpoint.Property(EndpointProperty.Scheme)}://{Endpoint.Property(EndpointProperty.HostAndPort)}");
}

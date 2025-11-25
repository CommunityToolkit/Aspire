namespace CommunityToolkit.Aspire.Hosting.Gcp.PubSub;

public class TopicResource(string name, PubSubResource pubSub, ReferenceExpression? topicId = null): Resource(name), IResourceWithParent<PubSubResource>, IResourceWithWaitSupport
{
    public ReferenceExpression TopicId { get; } = topicId ?? ReferenceExpression.Create($"{name}");

    public PubSubResource Parent { get; } = pubSub;

    internal List<SubscriberResource> Subscribers { get; } = [];

    private SubscriberServiceApiClient? _client;

    internal async ValueTask<SubscriberServiceApiClient> GetClientAsync(CancellationToken ct = default)
    {
        if (_client is not null)
        {
            return _client;
        }

        var builder = new SubscriberServiceApiClientBuilder()
        {
            Endpoint = Parent.Endpoint.Url,
            ChannelCredentials = ChannelCredentials.Insecure
        };
        _client = await builder.BuildAsync(ct);
        return _client;
    }
}

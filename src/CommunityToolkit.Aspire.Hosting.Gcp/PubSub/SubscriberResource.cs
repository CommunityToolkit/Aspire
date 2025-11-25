namespace CommunityToolkit.Aspire.Hosting.Gcp.PubSub;

public class SubscriberResource(string name, TopicResource topic, Action<Subscription>? configure, ReferenceExpression? subscriberId = null)
    : Resource(name), IResourceWithParent<TopicResource>, IResourceWithWaitSupport
{
    public TopicResource Parent { get; } = topic;
    internal Action<Subscription>? Configure { get; } = configure;
    public ReferenceExpression SubscriberId { get; } = subscriberId ?? ReferenceExpression.Create($"{name}");
}

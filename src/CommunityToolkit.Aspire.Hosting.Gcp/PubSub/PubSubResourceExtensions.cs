using System.Net;

namespace CommunityToolkit.Aspire.Hosting.Gcp.PubSub;

public static class PubSubResourceExtensions
{
    public static IResourceBuilder<PubSubResource> AddPubSub(this IDistributedApplicationBuilder builder,
        [ResourceName] string name, IResourceBuilder<ParameterResource> projectId)
    {
        var pubSub = new PubSubResource(name, projectId.Resource);

        var checkKey = $"{pubSub.Name}_check";

        var pubSubBuilder = builder.AddResource(pubSub)
            .WithImage(PubSubEmulatorContainerImage.Image, PubSubEmulatorContainerImage.Tag)
            .WithHttpEndpoint(targetPort: PubSubResource.Port, name: PubSubResource.EndpointName)
            .WithEntrypoint("gcloud")
            .WithArgs("beta", "emulators", "pubsub", "start", "--host-port", "0.0.0.0:" + PubSubResource.Port)
            .WithHealthCheck(checkKey)
            .WithEnvironment("PUBSUB_PROJECT_ID", projectId);

        builder.Services.AddHealthChecks().AddAsyncCheck(
            checkKey, async ct =>
            {
                var client = await pubSubBuilder.Resource.GetClientAsync(ct);
                try
                {
                    await client.ListTopicsAsync(new ListTopicsRequest
                    {
                        PageSize = 1,
                        ProjectAsProjectName = new ProjectName(await pubSub.ProjectId.GetValueAsync(ct))
                    }).AnyAsync(cancellationToken: ct);
                    return HealthCheckResult.Healthy();
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy(ex.Message, ex);
                }
            });

        return pubSubBuilder;
    }

    public static IResourceBuilder<TopicResource> AddTopic(this IResourceBuilder<PubSubResource> builder, [ResourceName] string name,
        ReferenceExpression? topicId = null)
    {
        var pubSub = builder.Resource;

        var topic = new TopicResource(name, pubSub, topicId);
        var topicBuilder = builder.ApplicationBuilder.AddResource(topic);
        pubSub.Topics.Add(topic);

        var checkKey = $"{topic.Name}_check";

        topicBuilder.WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = "Topic",
            CreationTimeStamp = DateTime.UtcNow,
            State = KnownResourceStates.NotStarted,
            Properties = []
        }).OnInitializeResource(static async (topicResource, initEvent, ct) =>
        {
            var log = initEvent.Logger;
            var eventing = initEvent.Eventing;
            var notification = initEvent.Notifications;
            var services = initEvent.Services;
            var topicId = await topicResource.TopicId.GetValueAsync(ct);
            if (topicId is null)
            {
                log.LogError("Couldn't allocate topic since no id provided! {topicResourceName}", topicResource.Name);
                await notification.PublishUpdateAsync(topicResource,
                    snapshot => snapshot with { State = KnownResourceStates.FailedToStart });

                return;
            }

            var pubSub = topicResource.Parent;
            await notification.PublishUpdateAsync(topicResource, snapshot =>
                snapshot with { State = KnownResourceStates.Waiting });
            await notification.WaitForResourceHealthyAsync(pubSub.Name, ct);
            await eventing.PublishAsync(new BeforeResourceStartedEvent(topicResource, services), ct);

            PublisherServiceApiClient client;
            try
            {
                client = await pubSub.GetClientAsync(ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to get pubsub client for pubsub: {pubSubResourceName}", pubSub.Name);
                await notification.PublishUpdateAsync(topicResource, snapshot =>
                    snapshot with { State = KnownResourceStates.FailedToStart });
                throw;
            }

            try
            {
                log.LogInformation("Creating topic {topicId} for resource: {topicResourceName}", topicId,
                    topicResource.Name);
                await client.CreateTopicAsync(new TopicName(await pubSub.ProjectId.GetValueAsync(ct), topicId));
                await notification.PublishUpdateAsync(topicResource, snapshot =>
                    snapshot with { State = KnownResourceStates.Running });
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.Conflict)
            {
                log.LogInformation("Topic: {topicId} for resource: {topicResourceName} already created", topicId,
                    topicResource.Name);
                await notification.PublishUpdateAsync(topicResource, snapshot =>
                    snapshot with { State = KnownResourceStates.Running });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to create topic: {topicId} for resource: {topicResourceName}", topicId, topicResource.Name);
                await notification.PublishUpdateAsync(topicResource, snapshot =>
                    snapshot with { State = KnownResourceStates.FailedToStart });
                throw;
            }
        }).WithHealthCheck(checkKey);


        topicBuilder.ApplicationBuilder.Services.AddHealthChecks().AddAsyncCheck(
            checkKey,
            async ct =>
            {
                var client = await pubSub.GetClientAsync(ct);
                var topicId = await topic.TopicId.GetValueAsync(ct)!;
                var projectId = await topic.Parent.ProjectId.GetValueAsync(ct)!;

                try
                {
                    await client.GetTopicAsync(new TopicName(projectId, topicId), ct);
                    return HealthCheckResult.Healthy();
                }
                catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    return HealthCheckResult.Unhealthy("Topic is not created yet");
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy(ex.Message, ex);
                }
            });

        return topicBuilder;
    }

    public static IResourceBuilder<SubscriberResource> AddSubscriber(this IResourceBuilder<TopicResource> builder,
        [ResourceName] string name,
        Action<Subscription>? configure = null,
        ReferenceExpression? subscriberId = null)
    {
        var topicResource = builder.Resource;

        var subscriber = new SubscriberResource(name, topicResource, configure, subscriberId);
        var subscriberBuilder = builder.ApplicationBuilder.AddResource(subscriber);
        topicResource.Subscribers.Add(subscriber);

        var checkKey = $"{subscriber.Name}_check";

        subscriberBuilder.WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = "Subscriber",
            CreationTimeStamp = DateTime.UtcNow,
            State = KnownResourceStates.NotStarted,
            Properties = []
        }).OnInitializeResource(static async (subscriberResource, initEvent, ct) =>
        {
            var log = initEvent.Logger;
            var eventing = initEvent.Eventing;
            var notification = initEvent.Notifications;
            var services = initEvent.Services;

            var subscriberId = await subscriberResource.SubscriberId.GetValueAsync(ct);
            if (subscriberId is null)
            {
                log.LogError("Couldn't allocate subscriber since no id provided! {subscriberResourceName}", subscriberResource.Name);
                await notification.PublishUpdateAsync(subscriberResource, snapshot => snapshot with { State = KnownResourceStates.FailedToStart });

                return;
            }

            var topic = subscriberResource.Parent;
            string projectId = await topic.Parent.ProjectId.GetValueAsync(ct)!;
            string topicId = await topic.TopicId.GetValueAsync(ct)!;

            await notification.PublishUpdateAsync(subscriberResource, snapshot => snapshot with { State = KnownResourceStates.Waiting });
            await notification.WaitForResourceHealthyAsync(topic.Name, ct);
            await eventing.PublishAsync(new BeforeResourceStartedEvent(subscriberResource, services), ct);

            SubscriberServiceApiClient client;
            try
            {
                client = await topic.GetClientAsync(ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to get pubsub client for topic: {topicResourceName}", topic.Name);
                await notification.PublishUpdateAsync(subscriberResource, snapshot => snapshot with { State = KnownResourceStates.FailedToStart });
                throw;
            }

            try
            {
                log.LogInformation("Creating subscriber {subscriberId} for resource: {subscriberResourceName}", subscriberId, subscriberResource.Name);
                var subscription = new Subscription
                {
                    SubscriptionName = new SubscriptionName(projectId, subscriberId),
                    TopicAsTopicName = new TopicName(projectId, topicId),
                };
                subscriberResource.Configure?.Invoke(subscription);
                await client.CreateSubscriptionAsync(subscription, ct);
                await notification.PublishUpdateAsync(subscriberResource, snapshot => snapshot with { State = KnownResourceStates.Running });
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.Conflict)
            {
                log.LogInformation("Subscriber: {subscriberId} for resource: {subscriberResourceName} already created", subscriberId, subscriberResource.Name);
                await notification.PublishUpdateAsync(subscriberResource, snapshot =>
                    snapshot with { State = KnownResourceStates.Running });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to create subscriber: {subscriberId} for resource: {subscriberResourceName}", subscriberId, subscriberResource.Name);
                await notification.PublishUpdateAsync(subscriberResource, snapshot => snapshot with { State = KnownResourceStates.FailedToStart });
                throw;
            }
        }).WithHealthCheck(checkKey);


        subscriberBuilder.ApplicationBuilder.Services.AddHealthChecks().AddAsyncCheck(
            checkKey,
            async ct =>
            {
                var client = await topicResource.GetClientAsync(ct);
                var subscriptionId = await subscriber.SubscriberId.GetValueAsync(ct)!;
                var projectId = await topicResource.Parent.ProjectId.GetValueAsync(ct)!;

                try
                {
                    await client.GetSubscriptionAsync(new SubscriptionName(projectId, subscriptionId), ct);
                    return HealthCheckResult.Healthy();
                }
                catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    return HealthCheckResult.Unhealthy("Subscriber is not created yet");
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy(ex.Message, ex);
                }
            });

        return subscriberBuilder;
    }
}

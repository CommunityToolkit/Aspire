using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CommunityToolkit.Aspire.Hosting.Gcp.Gcs;

public static class GcsResourceExtensions
{
    public static IResourceBuilder<GcsResource> AddGcs(this IDistributedApplicationBuilder builder, [ResourceName] string name, IResourceBuilder<ParameterResource> projectId, string certPath, string? initDataDirectory = null)
    {
        var gcs = new GcsResource(name, projectId.Resource);

        var gcsBuilder = builder.AddResource(gcs)
            .WithImage(GcsEmulatorContainerImageTags.Image, GcsEmulatorContainerImageTags.Tag)
            .WithHttpsEndpoint(name: "https", targetPort: GcsResource.TargetPort, port: GcsResource.TargetPort)
            .WithArgs("-external-url", $"https://localhost:{GcsResource.TargetPort}", "-public-host", "localhost", "-cert-location", "/app/cert/localhost.pem", "-private-key-location", "/app/cert/localhost.key")
            .WithDevCertificates("/app/cert/", "localhost.pem", "localhost.key")
            .WithHttpHealthCheck("/storage/v1/b", endpointName: "https")
            .WithEnvironment("GCS_PROJECT_ID", projectId)
            .WithIconName("Cloud");

        if (initDataDirectory is not null)
        {
            gcsBuilder.WithBindMount(initDataDirectory, GcsResource.InitializationPath, isReadOnly: false);
        }

        return gcsBuilder;
    }

    public static IResourceBuilder<GcsBucketResource> AddBucket(this IResourceBuilder<GcsResource> gcsBuilder, string bucketResourceName, ReferenceExpression? bucketName = null)
    {
        var gcs = gcsBuilder.Resource;
        var bucketResource = new GcsBucketResource(bucketResourceName, gcs, bucketName);
        var bucketBuilder = gcsBuilder.ApplicationBuilder.AddResource(bucketResource);
        gcs.Buckets.Add(bucketResource);
        bucketBuilder.WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = "Bucket",
            CreationTimeStamp = DateTime.UtcNow,
            State = KnownResourceStates.NotStarted,
            Properties = []
        }).OnInitializeResource(static async (bucketResource, initEvent, ct) =>
        {
            var log = initEvent.Logger;
            var eventing = initEvent.Eventing;
            var notification = initEvent.Notifications;
            var services = initEvent.Services;
            var bucketName = await bucketResource.BucketNameParameter.GetValueAsync(ct);
            if (bucketName is null)
            {
                log.LogError("Couldn't allocate bucket since no name provided! {bucketResourceName}", bucketResource.Name);
                await notification.PublishUpdateAsync(bucketResource,
                    snapshot => snapshot with { State = KnownResourceStates.FailedToStart });

                return;
            }

            var gcs = bucketResource.Parent;
            await notification.PublishUpdateAsync(bucketResource, snapshot =>
                snapshot with { State = KnownResourceStates.Waiting });
            await notification.WaitForResourceHealthyAsync(gcs.Name, ct);
            await eventing.PublishAsync(new BeforeResourceStartedEvent(bucketResource, services), ct);

            StorageClient client;
            try
            {
                client = await gcs.GetClientAsync(ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to get storage client for gcs: {gcsResourceName}", bucketResource.Parent.Name);
                await notification.PublishUpdateAsync(bucketResource, snapshot =>
                    snapshot with { State = KnownResourceStates.FailedToStart });
                throw;
            }

            try
            {
                log.LogInformation("Creating bucket: {bucketName} for resource: {bucketResourceName}", bucketName,
                    bucketResource.Name);
                await client.CreateBucketAsync(await gcs.ProjectId.GetValueAsync(ct), bucketName,
                    cancellationToken: ct);
                await notification.PublishUpdateAsync(bucketResource, snapshot =>
                    snapshot with { State = KnownResourceStates.Running });
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.Conflict)
            {
                log.LogInformation("Bucket: {bucketName} for resource: {bucketResourceName} already preinitialized", bucketName, bucketResource.Name);
                await notification.PublishUpdateAsync(bucketResource, snapshot =>
                    snapshot with { State = KnownResourceStates.Running });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to create bucket: {bucketName}", bucketResource.Name);
                await notification.PublishUpdateAsync(bucketResource, snapshot =>
                    snapshot with { State = KnownResourceStates.FailedToStart });
                throw;
            }
        }).WithIconName("CloudArchive");

        var checkKey = $"{bucketResource.Name}_check";
        bucketBuilder.ApplicationBuilder.Services.AddHealthChecks().AddAsyncCheck(
            checkKey,
            async ct =>
            {
                var client = await gcs.GetClientAsync(ct);
                var bucketName = await bucketResource.BucketNameParameter.GetValueAsync(ct);
                try
                {
                    await client.GetBucketAsync(bucketName, cancellationToken: ct);
                    return HealthCheckResult.Healthy();
                }
                catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
                {
                    return HealthCheckResult.Unhealthy("bucket is not created yet");
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy(ex.Message, ex);
                }
            });

        bucketBuilder.WithHealthCheck(checkKey);

        return bucketBuilder;
    }

    public static EndpointReference GetEndpoint(this IResourceBuilder<GcsResource> gcsBuilder)
    {
        return gcsBuilder.Resource.Endpoint;
    }

    public static IResourceBuilder<T> WithEnvironment<T>(this IResourceBuilder<T> builder, string environmentName,
        IResourceBuilder<GcsResource> gcs) where T : IResourceWithEnvironment
    {
        return builder.WithEnvironment(environmentName, gcs.GetEndpoint());
    }

    public static IResourceBuilder<T> WithEnvironment<T>(this IResourceBuilder<T> builder, string environmentName,
        IResourceBuilder<GcsBucketResource> bucketResource) where T : IResourceWithEnvironment
    {
        return builder.WithEnvironment(environmentName, bucketResource.Resource.BucketNameParameter);
    }
}

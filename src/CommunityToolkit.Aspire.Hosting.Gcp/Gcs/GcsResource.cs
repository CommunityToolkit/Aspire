using Aspire.Hosting.ApplicationModel;
using Google.Cloud.Storage.V1;
using System.Diagnostics.CodeAnalysis;

namespace CommunityToolkit.Aspire.Hosting.Gcp.Gcs;

public class GcsResource(string name, ParameterResource projectId)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string InitializationPath = "/data";
    private const string EndpointName = "https";
    internal const int TargetPort = 4443;

    internal List<GcsBucketResource> Buckets { get; } = [];
    public ParameterResource ProjectId { get; } = projectId;

    private StorageClient? _client;

    internal async ValueTask<StorageClient> GetClientAsync(CancellationToken ct = default)
    {
        if (_client is not null)
        {
            return _client;
        }

        var builder = new StorageClientBuilder
        {
            BaseUri = $"{Endpoint.Url}/storage/v1/"
        };
        _client = await builder.BuildAsync(ct);
        return _client;
    }

    [field: AllowNull, MaybeNull]
    public EndpointReference Endpoint => field ??= new EndpointReference(this, EndpointName);

    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Endpoint.Property(EndpointProperty.Scheme)}://{Endpoint.Property(EndpointProperty.HostAndPort)}");
}

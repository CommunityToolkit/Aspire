using Aspire.Hosting.ApplicationModel;

#pragma warning disable IDE0130
namespace Aspire.Hosting;
#pragma warning restore IDE0130

/// <summary>
/// A resource that represents a SeaweedFS container.
/// </summary>
/// <param name="name">The resource name.</param>
/// <param name="accessKey">The parameter containing the S3 access key.</param>
/// <param name="secretKey">The parameter containing the S3 secret key.</param>
public sealed class SeaweedFSContainerResource(string name, ParameterResource accessKey, ParameterResource secretKey)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string S3EndpointName = "s3";
    internal const string MasterEndpointName = "master";
    internal const string FilerEndpointName = "filer";
    internal const string VolumeEndpointName = "volume";

    /// <summary>Gets the S3 Access Key.</summary>
    public ParameterResource AccessKey { get; internal set; } = accessKey;

    /// <summary>Gets the S3 Secret Key.</summary>
    public ParameterResource SecretKey { get; internal set; } = secretKey;

    /// <summary>Gets the primary endpoint for the SeaweedFS S3 API. Will throw if S3 is not enabled.</summary>
    public EndpointReference PrimaryEndpoint => new(this, S3EndpointName);

    /// <summary>Gets the endpoint for the SeaweedFS Master API.</summary>
    public EndpointReference MasterEndpoint => new(this, MasterEndpointName);

    /// <summary>Gets the endpoint for the SeaweedFS Filer API. Will throw if Filer is not enabled.</summary>
    public EndpointReference FilerEndpoint => new(this, FilerEndpointName);

    /// <summary>Gets the connection string expression for the SeaweedFS resource.</summary>
    public ReferenceExpression ConnectionStringExpression => GetConnectionString();

    /// <summary>
    /// Gets the connection string asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A connection string for the SeaweedFS server.</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        if (this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out ConnectionStringRedirectAnnotation? connectionStringAnnotation))
        {
            return connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken);
        }

        return ConnectionStringExpression.GetValueAsync(cancellationToken);
    }

    private ReferenceExpression GetConnectionString()
    {
        ReferenceExpressionBuilder builder = new();

        bool hasS3 = Annotations.OfType<SeaweedFSS3Annotation>().Any();
        bool hasFiler = Annotations.OfType<SeaweedFSFilerAnnotation>().Any();

        EndpointReference targetEndpoint = hasS3 ? PrimaryEndpoint : MasterEndpoint;

        builder.Append($"Endpoint=http://{targetEndpoint.Property(EndpointProperty.Host)}:{targetEndpoint.Property(EndpointProperty.Port)}");

        if (hasS3)
        {
            builder.Append($";AccessKey={AccessKey}");
            builder.Append($";SecretKey={SecretKey}");
        }

        if (hasFiler)
        {
            builder.Append($";FilerEndpoint=http://{FilerEndpoint.Property(EndpointProperty.Host)}:{FilerEndpoint.Property(EndpointProperty.Port)}");
        }

        return builder.Build();
    }

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        bool hasS3 = Annotations.OfType<SeaweedFSS3Annotation>().Any();
        bool hasFiler = Annotations.OfType<SeaweedFSFilerAnnotation>().Any();

        EndpointReference targetEndpoint = hasS3 ? PrimaryEndpoint : MasterEndpoint;

        yield return new("Host", ReferenceExpression.Create($"{targetEndpoint.Property(EndpointProperty.Host)}"));
        yield return new("Port", ReferenceExpression.Create($"{targetEndpoint.Property(EndpointProperty.Port)}"));

        yield return new("MasterUrl", ReferenceExpression.Create($"http://{MasterEndpoint.Property(EndpointProperty.Host)}:{MasterEndpoint.Property(EndpointProperty.Port)}"));

        if (hasS3)
        {
            yield return new("AccessKey", ReferenceExpression.Create($"{AccessKey}"));
            yield return new("SecretKey", ReferenceExpression.Create($"{SecretKey}"));
            yield return new("S3Url", ReferenceExpression.Create($"http://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"));
        }

        if (hasFiler)
        {
            yield return new("FilerUrl", ReferenceExpression.Create($"http://{FilerEndpoint.Property(EndpointProperty.Host)}:{FilerEndpoint.Property(EndpointProperty.Port)}"));
        }
    }
}

// Internal annotations used safely during the resource compilation lifecycle
internal sealed class SeaweedFSS3Annotation : IResourceAnnotation { }
internal sealed class SeaweedFSFilerAnnotation : IResourceAnnotation { }
internal sealed class SeaweedFSCustomS3ConfigAnnotation(string hostPath) : IResourceAnnotation
{
    public string HostPath { get; } = hostPath;
}
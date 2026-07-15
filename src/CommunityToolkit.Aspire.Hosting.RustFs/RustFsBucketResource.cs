namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents an S3 bucket created on a RustFs container resource.
/// </summary>
public sealed class RustFsBucketResource : Resource, IResourceWithParent<RustFsResource>, IResourceWithConnectionString
{
    /// <summary>
    /// Gets the parent <see cref="RustFsResource"/> that hosts this bucket.
    /// </summary>
    public RustFsResource Parent { get; }

    /// <summary>
    /// Gets the S3 bucket name as it exists on the RustFs server.
    /// </summary>
    /// <remarks>
    /// This is the actual name used on the wire; it may differ from <see cref="Resource.Name"/>,
    /// which is the Aspire resource identifier.
    /// </remarks>
    public string BucketName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RustFsBucketResource"/> class.
    /// </summary>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="bucketName">The S3 bucket name on RustFs.</param>
    /// <param name="parent">The parent RustFs resource.</param>
    public RustFsBucketResource([ResourceName] string name, string bucketName, RustFsResource parent)
        : base(name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName);
        ArgumentNullException.ThrowIfNull(parent);

        BucketName = bucketName;
        Parent = parent;
    }

    /// <summary>
    /// Gets the connection string expression for the bucket.
    /// </summary>
    /// <remarks>
    /// Format: <c>Endpoint=http://host:port;AccessKey=key;SecretKey=secret;Bucket=bucket</c>.
    /// </remarks>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Parent};Bucket={BucketName}");

    /// <inheritdoc/>
    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        Parent.CombineProperties([
            new("Bucket", ReferenceExpression.Create($"{BucketName}"))
        ]);
}

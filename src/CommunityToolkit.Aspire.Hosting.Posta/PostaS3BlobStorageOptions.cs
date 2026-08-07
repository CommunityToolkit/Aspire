using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Parameter-based configuration options for Posta S3-compatible attachment storage.
/// </summary>
public sealed class PostaS3BlobStorageOptions
{
    /// <summary>
    /// Gets or sets the S3-compatible endpoint.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the S3 region.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Region { get; set; }

    /// <summary>
    /// Gets or sets the S3 bucket name.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Bucket { get; set; }

    /// <summary>
    /// Gets or sets the S3 access key.
    /// </summary>
    public IResourceBuilder<ParameterResource>? AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the S3 secret key.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SecretKey { get; set; }

    /// <summary>
    /// Gets or sets whether S3 storage uses TLS.
    /// </summary>
    public IResourceBuilder<ParameterResource>? UseSsl { get; set; }

    /// <summary>
    /// Gets or sets whether S3 path-style addressing is used.
    /// </summary>
    public IResourceBuilder<ParameterResource>? PathStyle { get; set; }
}

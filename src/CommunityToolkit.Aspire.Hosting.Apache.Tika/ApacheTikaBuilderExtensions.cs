using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Apache.Tika;

/// <summary>
/// A set of extension methods for adding Apache Tika resources to a distributed application builder.
/// </summary>
public static class ApacheTikaBuilderExtensions
{
    /// <summary>
    /// Adds an Apache Tika server resource to the distributed application builder.
    /// Apache Tika is a content analysis toolkit that detects and extracts metadata and text from various document types.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Apache Tika resource.</param>
    /// <returns>A resource builder for the Apache Tika resource.</returns>
    /// <remarks>
    /// This method configures an Apache Tika server with:
    /// - HTTP endpoint on port 9998
    /// - Health check on the /version endpoint
    /// - Default Apache Tika container image
    /// </remarks>
    public static IResourceBuilder<ApacheTikaResource> AddApacheTika(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new ApacheTikaResource(name);

        return builder.AddResource(resource)
            .WithImage(ApacheTikaContainerImageTags.Image, ApacheTikaContainerImageTags.Tag)
            .WithImageRegistry(ApacheTikaContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: 9998)
            .WithHttpHealthCheck("/version");
    }
}
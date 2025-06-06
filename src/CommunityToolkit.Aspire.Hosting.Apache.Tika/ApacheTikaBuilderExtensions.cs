using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Apache.Tika;

/// <summary>
/// A set of extension methods for adding Apache Tika resources to a distributed application builder.
/// </summary>
public static class ApacheTikaBuilderExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <returns></returns>
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
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Apache.Tika;

/// <summary>
/// A resource that represents an Apache Tika container.
/// </summary>
/// <see href="https://github.com/apache/tika-docker"/>
/// <param name="name"></param>
public class ApacheTikaResource(string name) : ContainerResource(name), IResourceWithEndpoints
{
}

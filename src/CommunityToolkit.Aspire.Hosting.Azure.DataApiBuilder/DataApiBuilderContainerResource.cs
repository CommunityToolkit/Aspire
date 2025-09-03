namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents Data Api Builder.
/// </summary>
public interface IDataApiBuilderResource : IResourceWithEnvironment, IResourceWithServiceDiscovery, IResourceWithWaitSupport { }

/// <summary>
/// A resource that represents Data Api Builder.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="entrypoint">An optional container entrypoint.</param>

public class DataApiBuilderContainerResource(string name, string? entrypoint = null)
    : ContainerResource(name, entrypoint), IDataApiBuilderResource
{
    internal const string HttpEndpointName = "http";
    internal const string HttpsEndpointName = "https";

    internal const int HttpEndpointPort = 5000;
    internal const int HttpsEndpointPort = 5001;

    internal int? HttpPort { get; set; }

    internal string[]? ConfigFilePaths { get; set; }
}

/// <summary>
/// A resource that represents Data Api Builder.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class DataApiBuilderExecutableResource(string name)
    : ExecutableResource(name, "dab", "./"), IDataApiBuilderResource
{
    internal const string HttpEndpointName = "http";
    internal const string HttpsEndpointName = "https";

    internal const int HttpEndpointPort = 5000;
    internal const int HttpsEndpointPort = 5001;
}
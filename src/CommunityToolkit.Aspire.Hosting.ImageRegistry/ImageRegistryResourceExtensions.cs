using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.ImageRegistry;


/// <summary>
/// Extension methods for adding an Image registry to the Aspire application
/// </summary>
public static class ImageRegistryResourceExtensions
{
    private const string RegistryNameEnvName = "REGISTRY_NAME";

    /// <summary>
    /// Adds a Docker registry container to the application
    /// </summary>
    public static IResourceBuilder<ContainerResource> AddDockerRegistry(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name, string volumeName)
    {
        return builder.AddContainer(name, "registry", "3")
            // isProxied must be false for host machine docker client to be able to push images to it
            .WithHttpEndpoint(targetPort: 5000, name: "http", isProxied: false)
            .WithHttpHealthCheck("/v2/", endpointName: "http")
            .WithVolume(volumeName, "/var/lib/registry")
            .WithOtlpExporter();
    }

    /// <summary>
    /// Adds a Dockerfile-based image to be built and pushed to the registry
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithRegistryDockerfile(
        this IResourceBuilder<ContainerResource> registry,
        string imageName,
        string imageNamePrefix,
        string dockerfileContext,
        string tag = "latest")
    {
        var builder = registry.ApplicationBuilder;

        if (!Directory.Exists(dockerfileContext))
        {
            throw new DirectoryNotFoundException($"Dockerfile context not found: {dockerfileContext}");
        }

        var fullImageName = $"${RegistryNameEnvName}/{imageNamePrefix}/{imageName}:{tag}";

        builder.AddExecutable($"{registry.Resource.Name}-{imageName}-image-builder", "/bin/sh", ".")
            .WithArgs("-c", $"echo \"REGISTRY_NAME: ${RegistryNameEnvName}\" && docker build -t {fullImageName} {dockerfileContext} && docker --config {Path.Join(builder.AppHostDirectory, "../docker-registry/docker-config/")} push {fullImageName}")
            .WithRegistry(registry)
            .WithIconName("BoxArrowUp");

        return registry;
    }

    /// <summary>
    /// Adds an existing image to be pulled and pushed to the registry
    /// </summary>
    public static IResourceBuilder<ContainerResource> WithRegistryImage(
        this IResourceBuilder<ContainerResource> registry,
        string imageName,
        string imageNamePrefix,
        string sourceImage,
        string tag = "latest")
    {
        var builder = registry.ApplicationBuilder;

        var fullImageName = $"${RegistryNameEnvName}/{imageNamePrefix}/{imageName}:{tag}";

        builder.AddExecutable($"{registry.Resource.Name}-{imageName}-image-publisher", "/bin/sh", builder.AppHostDirectory)
            .WithArgs("-c", $"echo \"REGISTRY_NAME: ${RegistryNameEnvName}\" && docker pull {sourceImage} && docker tag {sourceImage} {fullImageName} && docker push {fullImageName}")
            .WithRegistry(registry)
            .WithIconName("BoxArrowUp");

        return registry;
    }

    private static IResourceBuilder<ExecutableResource> WithRegistry(
        this IResourceBuilder<ExecutableResource> imageBuilder, IResourceBuilder<ContainerResource> registry)
    {
        return imageBuilder
            .WithEnvironment(RegistryNameEnvName, registry.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
            .WaitFor(registry)
            .WithParentRelationship(registry);
    }
}

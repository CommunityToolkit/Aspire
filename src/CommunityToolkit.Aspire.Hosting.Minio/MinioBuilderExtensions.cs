using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Minio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding MiniO resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class MinioBuilderExtensions
{
    private const string RootUserEnvVarName = "MINIO_ROOT_USER";
    private const string RootPasswordEnvVarName = "MINIO_ROOT_PASSWORD";

    /// <summary>
    /// Adds a MiniO container to the application model. The default image is "minio/minio" and the tag is "latest".
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="minioConsolePort">The host port for MinioO Admin.</param>
    /// <param name="minioPort">The host port for MiniO.</param>
    /// <param name="rootUser">The root user for the MiniO server.</param>
    /// <param name="rootPassword">The password for the MiniO root user.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{MinioContainerResource}"/>.</returns>
    public static IResourceBuilder<MinioContainerResource> AddMinioContainer(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource>? rootUser = null,
        IResourceBuilder<ParameterResource>? rootPassword = null,
        int minioPort = 9000,
        int minioConsolePort = 9001)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var rootPasswordParameter = rootPassword?.Resource ??
                           ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-rootPassword");

        var rootUserParameter = rootUser?.Resource ?? new ParameterResource("user", _ => MinioContainerResource.DefaultUserName);
        
        var minioContainer = new MinioContainerResource(name, rootUserParameter, rootPasswordParameter);

        var builderWithResource = builder
            .AddResource(minioContainer)
            .WithImage(MinioContainerImageTags.Image, MinioContainerImageTags.Tag)
            .WithImageRegistry(MinioContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: 9000, port: minioPort, name: MinioContainerResource.PrimaryEndpointName)
            .WithHttpEndpoint(targetPort: 9001, port: minioConsolePort, name: "console")
            .WithEnvironment("MINIO_ADDRESS", $":{minioPort.ToString()}")
            .WithEnvironment("MINIO_CONSOLE_ADDRESS", $":{minioConsolePort.ToString()}")
            .WithEnvironment(RootUserEnvVarName, minioContainer.RootUser.Value)
            .WithEnvironment(RootPasswordEnvVarName, minioContainer.RootPassword.Value)
            .WithArgs("server", "/data");

        var endpoint = builderWithResource.Resource.GetEndpoint(MinioContainerResource.PrimaryEndpointName);
        var healthCheckKey = $"{name}_check";
        
        builder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp =>
                {
                    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("miniohealth");
                    
                    return new MinioHealthCheck(endpoint.Url, httpClient);
                },
                failureStatus: default,
                tags: default,
                timeout: default));

        builderWithResource.WithHealthCheck(healthCheckKey);
        
        return builderWithResource;
    }
}
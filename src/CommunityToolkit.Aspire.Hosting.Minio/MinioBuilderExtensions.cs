using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Minio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding MinIO resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class MinioBuilderExtensions
{
    private const string RootUserEnvVarName = "MINIO_ROOT_USER";
    private const string RootPasswordEnvVarName = "MINIO_ROOT_PASSWORD";

    /// <summary>
    /// Adds a MinIO container to the application model. The default image is "minio/minio" and the tag is "latest".
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for MinIO.</param>
    /// <param name="rootUser">The parameter used to provide the root user name for the MinIO resource. If <see langword="null"/> a default value will be used.</param>
    /// <param name="rootPassword">The parameter used to provide the administrator password for the MinIO resource. If <see langword="null"/> a random password will be generated.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{MinioContainerResource}"/>.</returns>
    public static IResourceBuilder<MinioContainerResource> AddMinioContainer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? rootUser = null,
        IResourceBuilder<ParameterResource>? rootPassword = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        
        var rootPasswordParameter = rootPassword?.Resource ??
                           ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-rootPassword");

        var rootUserParameter = rootUser?.Resource ?? new ParameterResource("user", _ => MinioContainerResource.DefaultUserName);
        
        var resource = new MinioContainerResource(name, rootUserParameter, rootPasswordParameter);

        const int consoleTargetPort = 9001;
        var builderWithResource = builder
            .AddResource(resource)
            .WithImage(MinioContainerImageTags.Image, MinioContainerImageTags.Tag)
            .WithImageRegistry(MinioContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: 9000, port: port, name: MinioContainerResource.PrimaryEndpointName)
            .WithHttpEndpoint(targetPort: consoleTargetPort, name: MinioContainerResource.ConsoleEndpointName)
            .WithEnvironment(RootUserEnvVarName, resource.RootUser.Value)
            .WithEnvironment(RootPasswordEnvVarName, resource.PasswordParameter.Value)
            .WithArgs("server", "/data", "--console-address", $":{consoleTargetPort}");

        var endpoint = builderWithResource.Resource.GetEndpoint(MinioContainerResource.PrimaryEndpointName);
        var healthCheckKey = $"{name}_check";
        
        builder.Services.AddHealthChecks()
            .AddUrlGroup(options =>
            {
                var uri = new Uri(endpoint.Url);
                options.AddUri(new Uri(uri,"/minio/health/live"), setup => setup.ExpectHttpCode(200));
                options.AddUri(new Uri(uri, "/minio/health/cluster"), setup => setup.ExpectHttpCode(200));
                options.AddUri(new Uri(uri, "/minio/health/cluster/read"), setup => setup.ExpectHttpCode(200));
            }, healthCheckKey);

        builderWithResource.WithHealthCheck(healthCheckKey);
        
        return builderWithResource;
    }
    
    
    /// <summary>
    /// Configures the user name that the MinIO resource uses.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="userName">The parameter used to provide the user name for the MinIO resource.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<MinioContainerResource> WithUserName(this IResourceBuilder<MinioContainerResource> builder, IResourceBuilder<ParameterResource> userName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(userName);

        builder.Resource.RootUser = userName.Resource;
        return builder;
    }
    
    /// <summary>
    /// Configures the password that the MinIO resource is used.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="password">The parameter used to provide the password for the MinIO resource. If <see langword="null"/>, no password will be configured.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<MinioContainerResource> WithPassword(this IResourceBuilder<MinioContainerResource> builder, IResourceBuilder<ParameterResource> password)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.SetPassword(password.Resource);
        return builder;
    }
    
    /// <summary>
    /// Configures the host port that the MinIO resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for MinIO.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used, a random port will be assigned.</param>
    /// <returns>The resource builder for MinIO.</returns>
    public static IResourceBuilder<MinioContainerResource> WithHostPort(this IResourceBuilder<MinioContainerResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint("http", endpoint =>
        {
            endpoint.Port = port;
        });
    }
    
    /// <summary>
    /// Adds a named volume for the data folder to a MinIO container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an MinIO container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var minio = builder.AddMinio("minio")
    /// .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(minio);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<MinioContainerResource> WithDataVolume(this IResourceBuilder<MinioContainerResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/data");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a MinIO container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an MinIO container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var minio = builder.AddMinio("minio")
    /// .WithDataBindMount("./data/minio/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(minio);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<MinioContainerResource> WithDataBindMount(this IResourceBuilder<MinioContainerResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/data");
    }
}
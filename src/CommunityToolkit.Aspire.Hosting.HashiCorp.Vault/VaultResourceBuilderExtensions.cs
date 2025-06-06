using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.HashiCorp.Vault;

/// <summary>
/// Provides extension methods to simplify the configuration and use of HashiCorp Vault resources
/// in applications built using Aspire Hosting.
/// </summary>
public static class VaultResourceBuilderExtensions
{
    /// <summary>
    /// Adds a Vault resource to the distributed application builder with the specified name and optional port.
    /// </summary>
    /// <param name="builder">The distributed application builder to which the Vault resource will be added.</param>
    /// <param name="name">The name of the Vault resource.</param>
    /// <param name="port">The optional port on which the Vault resource will be configured. Defaults to 8200 if not specified.</param>
    /// <returns>An <see cref="IResourceBuilder{VaultResource}"/> instance that allows further configuration of the Vault resource.</returns>
    public static IResourceBuilder<VaultResource> AddVault(this IDistributedApplicationBuilder builder,
        [ResourceName] string name, int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(name, nameof(name));


        VaultResource resource = new(name);

        IHealthChecksBuilder health = builder.Services.AddHealthChecks();
        return builder.AddResource(resource)
            .WithContainerName($"heshicorp-{name}")
            .WithContainerRuntimeArgs("--cap-add", "IPC_LOCK")
            .WithAnnotation(new ContainerImageAnnotation
            {
                Image = VaultContainerImageTags.Image,
                Tag = VaultContainerImageTags.Tag,
                Registry = VaultContainerImageTags.Registry
            })
            .WithEndpoint(port ?? VaultResource.DefaultPort,
                VaultResource.DefaultPort,
                name: VaultResource.HttpEndpointName,
                scheme: VaultResource.HttpEndpointName)
            .WithEndpoint(VaultResource.DefaultPort, VaultResource.DefaultPort, name: "tcp", scheme: "tcp")
            .CheckHealth(health, name);
    }

    /// <summary>
    /// Configures the Vault resource with a data volume. The data volume is used to persist Vault's data
    /// across container restarts.
    /// </summary>
    /// <param name="builder">The resource builder to configure the Vault resource.</param>
    /// <param name="name">The optional name of the data volume. If not specified, a name will be generated automatically.</param>
    /// <returns>The updated resource builder with the data volume configuration added.</returns>
    public static IResourceBuilder<VaultResource> WithDataVolume(this IResourceBuilder<VaultResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "vault-data"), "/vault/data");
    }

    /// <summary>
    /// Configures the Vault resource to use a specific data volume with development mode settings.
    /// It ensures that the Vault instance uses a defined data volume path for storage and is optimized for development scenarios.
    /// </summary>
    /// <param name="builder">
    /// The resource builder used to configure the Vault resource.
    /// </param>
    /// <param name="name">
    /// Optional name of the data volume. If not provided, a default name is generated.
    /// </param>
    /// <returns>
    /// An updated resource builder configured with the specified data volume in development mode.
    /// </returns>
    public static IResourceBuilder<VaultResource> WithDataVolumeDevMode(this IResourceBuilder<VaultResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "vault-data"), "/vault/file");
    }

    /// <summary>
    /// Configures the Vault resource builder to operate in developer mode by setting
    /// the "VAULT_DEV_ROOT_TOKEN_ID" environment variable to "root".
    /// </summary>
    /// <param name="builder">
    /// An instance of <see cref="IResourceBuilder{VaultResource}"/> representing the Vault resource being configured.
    /// </param>
    /// <returns>
    /// The updated <see cref="IResourceBuilder{VaultResource}"/> instance with developer mode enabled.
    /// </returns>
    public static IResourceBuilder<VaultResource> WithDevMode(this IResourceBuilder<VaultResource> builder)
    {
        return builder.WithEnvironment("VAULT_DEV_ROOT_TOKEN_ID", "root");
    }


    /// <summary>
    /// Configures the Vault resource with the specified configuration file.
    /// </summary>
    /// <param name="builder">The resource builder for the Vault resource to configure.</param>
    /// <param name="filePath">The file path of the configuration directory where the configuration file is located.</param>
    /// <param name="fileName">The name of the configuration file. Defaults to "vault.hcl" if not provided.</param>
    /// <returns>An <see cref="IResourceBuilder{VaultResource}"/> instance configured with the specified settings.</returns>
    public static IResourceBuilder<VaultResource> WithConfig(this IResourceBuilder<VaultResource> builder,
        string filePath, string fileName = "vault.hcl")
    {
        string containerFile = $"{filePath}/{fileName}";
        return builder.WithBindMount("config/vault.hcl", containerFile, isReadOnly: true)
            .WithEnvironment("SKIP_CHOWN", "true")
            .WithArgs("server", $"-config={containerFile}");
    }

    /// <summary>
    /// Adds health check configurations for a Vault resource by setting up monitoring endpoints
    /// and associating health check rules with them.
    /// </summary>
    /// <param name="builder">The resource builder for the Vault resource.</param>
    /// <param name="health">The health checks builder used to configure health checks.</param>
    /// <param name="name">The name of the Vault resource to be used in health check identification.</param>
    /// <returns>The resource builder for the Vault resource with health check settings applied.</returns>
    private static IResourceBuilder<VaultResource> CheckHealth(this IResourceBuilder<VaultResource> builder,
        IHealthChecksBuilder health, string name)
    {
        EndpointReference endpointHttp = builder.GetEndpoint(VaultResource.HttpEndpointName);

        health.AddUrlGroup(x => new Uri($"{endpointHttp.Url}/v1/sys/health"),
            $"{name}-sys-health",
            HealthStatus.Unhealthy);
        health.AddUrlGroup(x => new Uri($"{endpointHttp.Url}/v1/sys/leader"),
            $"{name}-sys-leader",
            HealthStatus.Degraded);
        builder.WithHealthCheck($"{name}-sys-health")
            .WithHealthCheck($"{name}-sys-leader");
        return builder;
    }
}
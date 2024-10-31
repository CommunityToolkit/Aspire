using System.Data.Common;
using CommunityToolkit.Aspire.Hosting.Meilisearch;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Meilisearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CommunityToolkit.Aspire.Meilisearch;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Meilisearch resources to the application model.
/// </summary>
public static class MeilisearchBuilderExtensions
{
    private const int MeilisearchPort = 7700;
    private const int MeilisearchUIPort = 24900;

    /// <summary>
    /// Adds an Meilisearch container resource to the application model.
    /// The default image is <inheritdoc cref="MeilisearchContainerImageTags.Image"/> and the tag is <inheritdoc cref="MeilisearchContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <param name="masterKey">The parameter used to provide the master key for the Meilisearch. If <see langword="null"/> a random master key will be generated.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an Meilisearch container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var meilisearch = builder.AddMeilisearch("meilisearch");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(meilisearch);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<MeilisearchResource> AddMeilisearch(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource>? masterKey = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var masterKeyParameter = masterKey?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-masterKey");

        var meilisearch = new MeilisearchResource(name, masterKeyParameter);

        MeilisearchClient? meilisearchClient = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(meilisearch, async (@event, ct) =>
        {
            var connectionString = await meilisearch.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false)
            ?? throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{meilisearch.Name}' resource but the connection string was null.");

            meilisearchClient = CreateMeilisearchClient(connectionString);
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
         .Add(new HealthCheckRegistration(
             healthCheckKey,
             sp => new MeilisearchHealthCheck(meilisearchClient!),
             failureStatus: default,
             tags: default,
             timeout: default));

        return builder.AddResource(meilisearch)
             .WithImage(MeilisearchContainerImageTags.Image, MeilisearchContainerImageTags.Tag)
             .WithImageRegistry(MeilisearchContainerImageTags.Registry)
             .WithHttpEndpoint(targetPort: MeilisearchPort, port: port, name: MeilisearchResource.PrimaryEndpointName)
             .WithEnvironment(context =>
             {
                 context.EnvironmentVariables["MEILI_MASTER_KEY"] = meilisearch.MasterKeyParameter;
             })
             .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a Meilisearch container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an Meilisearch container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var meilisearch = builder.AddMeilisearch("meilisearch")
    /// .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(meilisearch);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<MeilisearchResource> WithDataVolume(this IResourceBuilder<MeilisearchResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

#pragma warning disable CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "data"), "/meili_data");
#pragma warning restore CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Meilisearch container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an Meilisearch container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var meilisearch = builder.AddMeilisearch("meilisearch")
    /// .WithDataBindMount("./data/meilisearch/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(meilisearch);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<MeilisearchResource> WithDataBindMount(this IResourceBuilder<MeilisearchResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/meili_data");
    }

    /// <summary>
    /// Adds an administration and development platform for Meilisearch to the application model using Meilisearch-UI.
    /// </summary>
    /// <remarks>
    /// This version of the package defaults to the <inheritdoc cref="MeilisearchContainerImageTags.UITag"/> tag of the <inheritdoc cref="MeilisearchContainerImageTags.UIImage"/> container image.
    /// </remarks>
    /// <example>
    /// Use in application host with a Meilisearch resource
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var meilisearch = builder.AddMeilisearch("meilisearch")
    ///   .WithMeilisearchUI();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(meilisearch);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    /// <param name="builder">The Meilisearch server resource builder.</param>
    /// <param name="configureContainer">Configuration callback for Meilisearch-UI container resource.</param>
    /// <param name="containerName">The name of the container (Optional).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithMeilisearchUI<T>(this IResourceBuilder<T> builder, Action<IResourceBuilder<MeilisearchResourceUI>>? configureContainer = null, string? containerName = null) where T : MeilisearchResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        containerName ??= $"{builder.Resource.Name}-ui";

        var meilisearchUI = new MeilisearchResourceUI(containerName);
        var resourceBuilder = builder.ApplicationBuilder.AddResource(meilisearchUI)
                                                        .WithImage(MeilisearchContainerImageTags.UIImage, MeilisearchContainerImageTags.UITag)
                                                        .WithImageRegistry(MeilisearchContainerImageTags.Registry)
                                                        .WithHttpEndpoint(targetPort: MeilisearchUIPort, name: MeilisearchResourceUI.PrimaryEndpointName)
                                                        .WithEnvironment(context => ConfigureUIContainer(context, builder.Resource))
                                                        .ExcludeFromManifest();

        configureContainer?.Invoke(resourceBuilder);

        return builder;
    }

    /// <summary>
    /// Configures the host port that the Meilisearch UI resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for Meilisearch UI.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The resource builder for Meilisearch UI.</returns>
    public static IResourceBuilder<MeilisearchResourceUI> WithHostPort(this IResourceBuilder<MeilisearchResourceUI> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(MeilisearchResource.PrimaryEndpointName, endpoint =>
        {
            endpoint.Port = port;
        });
    }

    private static void ConfigureUIContainer(EnvironmentCallbackContext context, MeilisearchResource resource)
    {
        context.EnvironmentVariables.Add("VITE_SINGLETON_MODE", "true");
        context.EnvironmentVariables.Add("VITE_SINGLETON_HOST", $"{resource.PrimaryEndpoint.Scheme}://{resource.Name}:{resource.PrimaryEndpoint.TargetPort}");
        context.EnvironmentVariables.Add("VITE_SINGLETON_API_KEY", resource.MasterKeyParameter.Value);
    }

    internal static MeilisearchClient CreateMeilisearchClient(string? connectionString)
    {
        if (connectionString is null)
        {
            throw new InvalidOperationException("Connection string is unavailable");
        }

        Uri? endpoint = null;
        string? masterKey = null;

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            endpoint = uri;
        }
        else
        {
            var connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.TryGetValue("Endpoint", out var endpointValue) && Uri.TryCreate(endpointValue.ToString(), UriKind.Absolute, out var serviceUri))
            {
                endpoint = serviceUri;
            }

            if (connectionBuilder.TryGetValue("MasterKey", out var masterKeyValue))
            {
                masterKey = masterKeyValue.ToString();
            }
        }

        return new MeilisearchClient(endpoint!.ToString(), apiKey: masterKey!);
    }
}

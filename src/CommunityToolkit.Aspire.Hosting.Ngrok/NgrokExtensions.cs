using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Ngrok;
using System.Runtime.InteropServices;
using System.Text;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding ngrok to the application model.
/// </summary>
public static class NgrokExtensions
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static readonly bool IsOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Configures a container resource for grok which is pre-configured to connect to the resource that this method is used on.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configurationFolder">The folder where temporary ngrok configuration files will be stored; defaults to .ngrok</param>
    /// <param name="endpointPort">The port of the endpoint for this resource, defaults to a randomly assigned port.</param>
    /// <param name="endpointName">The name of the endpoint for this resource, defaults to http.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TResource}"/>.</returns>
    public static IResourceBuilder<NgrokResource> AddNgrok(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string? configurationFolder = null,
        int? endpointPort = null,
        [EndpointName] string? endpointName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (configurationFolder is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationFolder);
        
        if (endpointPort is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(endpointPort.Value, 1, nameof(endpointPort));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(endpointPort.Value, 65535, nameof(endpointPort));
        }

        configurationFolder ??= Path.Combine(builder.AppHostDirectory, ".ngrok");
        if (!Directory.Exists(configurationFolder))
            Directory.CreateDirectory(configurationFolder);
        
        var resource = new NgrokResource(name);
        var resourceBuilder = builder.AddResource(resource)
            .WithImage(NgrokContainerValues.Image, NgrokContainerValues.Tag)
            .WithImageRegistry(NgrokContainerValues.Registry)
            .WithBindMount(configurationFolder, "/var/tmp/ngrok")
            .WithHttpEndpoint(targetPort: 4040, port: endpointPort, name: endpointName);
        builder.Eventing.Subscribe<AfterEndpointsAllocatedEvent>(async (e, ct) =>
        {
            var endpointTuples = resource.Annotations
                .OfType<NgrokEndpointAnnotation>()
                .SelectMany(annotation => annotation.Endpoints.Select(ngrokEndpoint => (endpointRefernce: annotation.Resource.GetEndpoint(ngrokEndpoint.EndpointName), ngrokEndpoint)))
                .ToList();
            
            var startParameter = "--none";
            if (endpointTuples.Count > 0)
            {
                await CreateNgrokConfigurationFileAsync(configurationFolder, name, endpointTuples);
                startParameter = "--all";
            }
            resourceBuilder
                .WithArgs("start", startParameter, "--config", $"/var/tmp/ngrok/{name}.yml");
        });
        return resourceBuilder;
    }
    
    /// <summary>
    /// Adds a ngrok auth token to a ngrok resource.
    /// </summary>
    /// <param name="builder">The ngrok resource builder.</param>
    /// <param name="ngrokAuthToken">The ngrok auth token.</param>
    /// <returns>The same reference to ngrok resource builder.</returns>
    public static IResourceBuilder<NgrokResource> WithAuthToken(
        this IResourceBuilder<NgrokResource> builder,
        string ngrokAuthToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(ngrokAuthToken);

        return builder.WithEnvironment(NgrokContainerValues.AuthTokenEnvName, ngrokAuthToken);
    }
    
    /// <summary>
    /// Adds a ngrok auth token to a ngrok resource.
    /// </summary>
    /// <param name="builder">The ngrok resource builder.</param>
    /// <param name="ngrokAuthToken">The ngrok auth token as a parameter resource.</param>
    /// <returns>The same reference to ngrok resource builder.</returns>
    public static IResourceBuilder<NgrokResource> WithAuthToken(
        this IResourceBuilder<NgrokResource> builder,
        IResourceBuilder<ParameterResource> ngrokAuthToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(ngrokAuthToken);

        return builder.WithEnvironment(NgrokContainerValues.AuthTokenEnvName, ngrokAuthToken);
    }
    
    /// <summary>
    /// Configures a resource with endpoints as a ngrok tunnel endpoint.
    /// </summary>
    /// <typeparam name="TResource">The resource type.</typeparam>
    public static IResourceBuilder<NgrokResource> WithTunnelEndpoint<TResource>(
        this IResourceBuilder<NgrokResource> builder, 
        IResourceBuilder<TResource> resource,
        string endpointName,
        string? ngrokUrl = null) where TResource : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        if (ngrokUrl is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(ngrokUrl);
        
        var existingAnnotation = builder.Resource.Annotations
            .OfType<NgrokEndpointAnnotation>()
            .SingleOrDefault(a => a.Resource.Name == resource.Resource.Name);
        if (existingAnnotation is not null)
        {
            existingAnnotation.Endpoints.Add(new NgrokEndpoint(endpointName, ngrokUrl));
        }
        else
        {
            var newAnnotation = new NgrokEndpointAnnotation(resource.Resource);
            newAnnotation.Endpoints.Add(new NgrokEndpoint(endpointName, ngrokUrl));
            builder.Resource.Annotations.Add(newAnnotation);
        }

        return builder;
    }
    
    private static async Task CreateNgrokConfigurationFileAsync(
        string configurationFolder,
        string name,
        IEnumerable<(EndpointReference, NgrokEndpoint)> endpointTuples)
    {
        var ngrokConfig = new StringBuilder();
        ngrokConfig.AppendLine("version: 3");
        ngrokConfig.AppendLine();
        ngrokConfig.AppendLine("agent:");
        ngrokConfig.AppendLine( "  log: stdout");
        ngrokConfig.AppendLine();
        ngrokConfig.AppendLine("endpoints:");
        foreach (var (endpointReference, ngrokEndpoint) in endpointTuples)
        {
            ngrokConfig.AppendLine($"  - name: {endpointReference.Resource.Name}-{endpointReference.EndpointName}");
            if (!string.IsNullOrWhiteSpace(ngrokEndpoint.Url))
                ngrokConfig.AppendLine($"    url: {ngrokEndpoint.Url}");    
            ngrokConfig.AppendLine( "    upstream:");
            ngrokConfig.AppendLine($"      url: {GetUpstreamUrl(endpointReference)}");
        }

        await File.WriteAllTextAsync(Path.Combine(configurationFolder, $"{name}.yml"), ngrokConfig.ToString());
    }

    private static string GetUpstreamUrl(EndpointReference endpoint)
    {
        var isLocal = endpoint.Host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase);
        var host = (IsWindows || IsOsx) && isLocal? "host.docker.internal" : endpoint.Host;
        return $"{endpoint.Scheme}://{host}:{endpoint.Port}";
    }
}
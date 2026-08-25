using Aspire.Hosting.ApplicationModel;
using Zemires.Aspire.Hosting.N8n;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding N8n resources to the application model.
/// </summary>
public static partial class N8nBuilderExtensions
{
    private const int N8nRunnersPort = 5680;
    private const int N8nBrokerPort = 5679;
    private const string BrokerEndpointName = "broker";

    /// <summary>
    /// Adds a task runner instance for the given N8n resource as sidecar. Aspire does currently not support native sidecar pattern.
    /// </summary>
    /// <param name="n8nBuilder">The primary N8n resource builder to attach the worker to.</param>
    /// <param name="name">The name to use for the worker resource.</param>
    /// <param name="port">The port to use for the worker resource.</param>
    /// <param name="sharedAuthToken">The authentication token to share with the broker resource.</param>
    /// <returns>A new <see cref="IResourceBuilder{N8nTaskRunnerResource}"/> for the worker instance.</returns>
    [AspireExport]
    public static IResourceBuilder<N8nTaskRunnerResource> AddTaskRunner(this IResourceBuilder<N8nResource> n8nBuilder, 
        string name,
        int? port = null,
        IResourceBuilder<ParameterResource>? sharedAuthToken = null)
    {
        ArgumentNullException.ThrowIfNull(n8nBuilder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var sharedAuthTokenParameter = sharedAuthToken?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(n8nBuilder.ApplicationBuilder, $"{n8nBuilder.Resource.Name}-{name}-runners-auth-token");

        n8nBuilder.WithEnvironment("N8N_RUNNERS_MODE", "external")
            .WithEnvironment("N8N_RUNNERS_BROKER_LISTEN_ADDRESS", "0.0.0.0")
            .WithEnvironment("N8N_RUNNERS_AUTH_TOKEN", sharedAuthTokenParameter)
            .WithHttpEndpoint(targetPort: N8nBrokerPort, name: BrokerEndpointName);

        var runner = new N8nTaskRunnerResource(n8nBuilder.Resource.Name + "-" + name, n8nBuilder.Resource);

        var brokerEndpoint = n8nBuilder.Resource.GetEndpoint(BrokerEndpointName);

        var runnerBuilder = n8nBuilder.ApplicationBuilder.AddResource(runner)
            .WithAnnotation(new ContainerImageAnnotation { Image = N8nContainerImageTags.ImageRunners, Tag = N8nContainerImageTags.Tag, Registry = N8nContainerImageTags.Registry })
            .WithIconName("SettingsCogMultiple", IconVariant.Filled)
            .WithHttpEndpoint(targetPort: N8nRunnersPort, port: port, name: N8nResource.PrimaryEndpointName, env: "N8N_PORT")
            .WithHttpHealthCheck("/healthz", 200, N8nResource.PrimaryEndpointName)
            .WithEnvironment("N8N_RUNNERS_AUTH_TOKEN", sharedAuthTokenParameter)
            .WithEnvironment("N8N_RUNNERS_TASK_BROKER_URI", brokerEndpoint)
            .WithParentRelationship(n8nBuilder);

        return runnerBuilder;
    }
}
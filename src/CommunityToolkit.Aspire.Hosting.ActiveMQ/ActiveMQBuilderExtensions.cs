// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding ActiveMQ resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class ActiveMQBuilderExtensions
{
    /// <summary>
    /// Adds a ActiveMQ container to the application model.
    /// </summary>
    /// <remarks>
    /// The default image and tag are "apache/activemq-classic" and "6.1.0".
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the username for the ActiveMQ resource. If <see langword="null"/> a default value will be used.</param>
    /// <param name="password">The parameter used to provide the password for the ActiveMQ resource. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="port">The host port that the underlying container is bound to when running locally.</param>
    /// <param name="scheme">The scheme of the endpoint, e.g. tcp or activemq (for masstransit). Defaults to tcp.</param>
    /// <param name="webPort">The host port that the underlying webconsole is bound to when running locally.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ActiveMQServerResource> AddActiveMQ(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        int? port = null,
        string scheme = "tcp",
        int? webPort = null)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(scheme, nameof(scheme));
        
        // don't use special characters in the password, since it goes into a URI
        ParameterResource passwordParameter = password?.Resource
                                              ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", special: false);

        ActiveMQServerResource activeMq = new(name, userName?.Resource, passwordParameter, scheme);
        return builder.Build(port, scheme, webPort, activeMq);
    }

    /// <summary>
    /// Adds a ActiveMQ Artemis container to the application model.
    /// </summary>
    /// <remarks>
    /// The default image and tag are "apache/activemq-artemis" and "2.39.0".
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the username for the ActiveMQ resource. If <see langword="null"/> a default value will be used.</param>
    /// <param name="password">The parameter used to provide the password for the ActiveMQ resource. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="port">The host port that the underlying container is bound to when running locally.</param>
    /// <param name="scheme">The scheme of the endpoint, e.g. tcp or activemq (for masstransit). Defaults to tcp.</param>
    /// <param name="webPort">The host port that the underlying webconsole is bound to when running locally.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ActiveMQArtemisServerResource> AddActiveMQArtemis(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        int? port = null,
        string scheme = "tcp",
        int? webPort = null)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));
        ArgumentNullException.ThrowIfNull(scheme, nameof(scheme));
        
        // don't use special characters in the password, since it goes into a URI
        ParameterResource passwordParameter = password?.Resource
                                              ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", special: false);

        ActiveMQArtemisServerResource activeMq = new(name, userName?.Resource, passwordParameter, scheme);
        return builder.Build(port, scheme, webPort, activeMq);
    }

    private static IResourceBuilder<T> Build<T>(this IDistributedApplicationBuilder builder, int? port, string scheme, int? webPort, T activeMq)
    where T : ActiveMQServerResourceBase
    {
        IResourceBuilder<T> result = builder.AddResource(activeMq)
            .WithImage(activeMq.ActiveMqSettings.Image, activeMq.ActiveMqSettings.Tag)
            .WithImageRegistry(activeMq.ActiveMqSettings.Registry)
            .WithEndpoint(port: port, targetPort: 61616, name: ActiveMQServerResourceBase.PrimaryEndpointName, scheme: scheme)
            .WithEndpoint(port: webPort, targetPort: 8161, name: "web", scheme: "http")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables[activeMq.ActiveMqSettings.EnvironmentVariableUsername] = activeMq.UserNameReference;
                context.EnvironmentVariables[activeMq.ActiveMqSettings.EnvironmentVariablePassword] = activeMq.PasswordParameter;
            });
        return result.WithJolokiaHealthCheck();
    }

    /// <summary>
    /// Adds a named volume for the data folder to a ActiveMQ container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithDataVolume<T>(this IResourceBuilder<T> builder, string? name = null, bool isReadOnly = false)
        where T : ActiveMQServerResourceBase =>
        builder
            .WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"),
                builder.Resource.ActiveMqSettings.DataPath,
                isReadOnly);

    /// <summary>
    /// Adds a named volume for the config folder to a ActiveMQ container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithConfVolume<T>(this IResourceBuilder<T> builder, string? name = null, bool isReadOnly = false)
        where T : ActiveMQServerResourceBase =>
        builder
            .WithVolume(name ?? VolumeNameGenerator.Generate(builder, "conf"),
                builder.Resource.ActiveMqSettings.ConfPath,
                isReadOnly);

    /// <summary>
    /// Adds a bind mount for the data folder to a ActiveMQ container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithDataBindMount<T>(this IResourceBuilder<T> builder, string source, bool isReadOnly = false) 
        where T : ActiveMQServerResourceBase =>
        builder.WithBindMount(source, builder.Resource.ActiveMqSettings.DataPath, isReadOnly);

    /// <summary>
    /// Adds a bind mount for the conf folder to a ActiveMQ container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithConfBindMount<T>(this IResourceBuilder<T> builder, string source, bool isReadOnly = false)
        where T : ActiveMQServerResourceBase =>
        builder.WithBindMount(source, builder.Resource.ActiveMqSettings.ConfPath, isReadOnly);
    
    private static IResourceBuilder<T> WithJolokiaHealthCheck<T>(
        this IResourceBuilder<T> builder)
    where T : ActiveMQServerResourceBase
    {
        const int statusCode = 200;
        const string endpointName = "web";
        const string scheme = "http";
        EndpointReference endpoint = builder.Resource.GetEndpoint(endpointName);

        builder.ApplicationBuilder.Eventing.Subscribe<AfterEndpointsAllocatedEvent>((_, _) =>
        {
            if (!endpoint.Exists)
            {
                throw new DistributedApplicationException($"The endpoint '{endpointName}' does not exist on the resource '{builder.Resource.Name}'.");
            }

            if (endpoint.Scheme != scheme)
            {
                throw new DistributedApplicationException($"The endpoint '{endpointName}' on resource '{builder.Resource.Name}' was not using the '{scheme}' scheme.");
            }

            return Task.CompletedTask;
        });

        Uri? uri = null;
        string basicAuthentication = string.Empty;
        builder.ApplicationBuilder.Eventing.Subscribe<BeforeResourceStartedEvent>(builder.Resource, async (_, ct) =>
        {
            Uri baseUri = new Uri(endpoint.Url, UriKind.Absolute);
            string userName = (await builder.Resource.UserNameReference.GetValueAsync(ct))!;
            string password = builder.Resource.PasswordParameter.Value;
            basicAuthentication = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));
            uri = new UriBuilder(baseUri)
            {
                Path = builder.Resource.ActiveMqSettings.JolokiaPath
            }.Uri;
        });

        string healthCheckKey = $"{builder.Resource.Name}_{endpointName}_check";
        builder.ApplicationBuilder.Services.AddLogging(configure =>
        {
            // The AddUrlGroup health check makes use of http client factory.
            configure.AddFilter($"System.Net.Http.HttpClient.{healthCheckKey}.LogicalHandler", LogLevel.None);
            configure.AddFilter($"System.Net.Http.HttpClient.{healthCheckKey}.ClientHandler", LogLevel.None);
        });

        builder.ApplicationBuilder.Services.AddHealthChecks().AddUrlGroup(options =>
        {
            if (uri is null)
            {
                throw new DistributedApplicationException($"The URI for the health check is not set. Ensure that the resource has been allocated before the health check is executed.");
            }

            options.AddUri(uri, setup =>
            {
                setup.AddCustomHeader("Authorization", basicAuthentication);
                setup.AddCustomHeader("origin", "localhost");
                setup.ExpectHttpCode(statusCode);
            });
        }, healthCheckKey);

        builder.WithHealthCheck(healthCheckKey);

        return builder;
    }
}

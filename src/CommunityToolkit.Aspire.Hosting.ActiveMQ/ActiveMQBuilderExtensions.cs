// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Sockets;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

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
    /// The default image and tag are "apache/activemq" and "6.3.2".
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the username for the ActiveMQ resource. If <see langword="null"/> a default value will be used.</param>
    /// <param name="password">The parameter used to provide the password for the ActiveMQ resource. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="port">The host port that the underlying container is bound to when running locally.</param>
    /// <param name="scheme">The scheme of the endpoint, e.g. tcp or activemq (for masstransit). Defaults to tcp.</param>
    /// <param name="webPort">The host port that the underlying webconsole is bound to when running locally.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ActiveMQServerResource> AddActiveMQ(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        int? port = null,
        string scheme = "tcp",
        int? webPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
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
    /// The default image and tag are "apache/artemis" and "2.57.0".
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the username for the ActiveMQ resource. If <see langword="null"/> a default value will be used.</param>
    /// <param name="password">The parameter used to provide the password for the ActiveMQ resource. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="port">The host port that the underlying container is bound to when running locally.</param>
    /// <param name="scheme">The scheme of the endpoint, e.g. tcp or activemq (for masstransit). Defaults to tcp.</param>
    /// <param name="webPort">The host port that the underlying webconsole is bound to when running locally.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
    public static IResourceBuilder<ActiveMQArtemisServerResource> AddActiveMQArtemis(this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        int? port = null,
        string scheme = "tcp",
        int? webPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
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
            .WithIconName("MailMultiple")
            .WithEndpoint(port: port, targetPort: 61616, name: ActiveMQServerResourceBase.PrimaryEndpointName, scheme: scheme)
            .WithEndpoint(port: webPort, targetPort: 8161, name: "web", scheme: "http")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables[activeMq.ActiveMqSettings.EnvironmentVariableUsername] = activeMq.UserNameReference;
                context.EnvironmentVariables[activeMq.ActiveMqSettings.EnvironmentVariablePassword] = activeMq.PasswordParameter;
            });
        return result.WithBrokerHealthCheck();
    }

    /// <summary>
    /// Adds a named volume for the data folder to a ActiveMQ container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExport]
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
    [AspireExport]
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
    [AspireExport]
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
    [AspireExport]
    public static IResourceBuilder<T> WithConfBindMount<T>(this IResourceBuilder<T> builder, string source, bool isReadOnly = false)
        where T : ActiveMQServerResourceBase =>
        builder.WithBindMount(source, builder.Resource.ActiveMqSettings.ConfPath, isReadOnly);

    private static IResourceBuilder<T> WithBrokerHealthCheck<T>(
        this IResourceBuilder<T> builder)
    where T : ActiveMQServerResourceBase
    {
        const string endpointName = ActiveMQServerResourceBase.PrimaryEndpointName;
        EndpointReference endpoint = builder.Resource.PrimaryEndpoint;
        string healthCheckKey = $"{builder.Resource.Name}_{endpointName}_check";

        builder.ApplicationBuilder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            healthCheckKey,
            _ => new ActiveMQHealthCheck(endpoint),
            failureStatus: HealthStatus.Unhealthy,
            tags: null));

        builder.WithHealthCheck(healthCheckKey);

        return builder;
    }
}

internal sealed class ActiveMQHealthCheck(EndpointReference endpoint) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!endpoint.IsAllocated)
        {
            return HealthCheckResult.Unhealthy("The ActiveMQ endpoint has not been allocated.");
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to connect to the ActiveMQ broker.", ex);
        }
    }
}

#pragma warning restore ASPIREATS001

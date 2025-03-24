// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Zitadel;

/// <summary>
/// Provides extension methods for adding Zitadel resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class ZitadelResourceBuilderExtensions
{
    private const string AdminEnvVarName = "ZITADEL_DEFAULTINSTANCE_ORG_HUMAN_USERNAME";
    private const string AdminEnvVarPassword = "ZITADEL_DEFAULTINSTANCE_ORG_HUMAN_PASSWORD";

    private const int DefaultcontainerPort = 8080; // ZITADEL_PORT
    private const string ManagementEndpointName = "management";

    /// <summary>
    /// Adds a Zitadel container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. </param>
    /// <param name="port">The host port that the underlying container is bound to when running locally.</param>
    /// <param name="adminUsername">The parameter used as the admin for the Zitadel resource. If <see langword="null"/> a default value will be used.</param>
    /// <param name="adminPassword">The parameter used as the admin password for the Zitadel resource. If <see langword="null"/> a default password will be used.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ZitadelResource> AddZitadel
    (
        this IDistributedApplicationBuilder builder,
        string name,
        int? port = null,
        IResourceBuilder<ParameterResource>? adminUsername = null,
        IResourceBuilder<ParameterResource>? adminPassword = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var passwordParameter = adminPassword?.Resource ??
                                ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");
        
        var resource = new ZitadelResource(name, adminUsername?.Resource, passwordParameter);

        // TODO: Add health check
        var zitadel = builder
            .AddResource(resource)
            .WithImage(ZitadelContainerImageTags.Image)
            .WithImageRegistry(ZitadelContainerImageTags.Registry)
            .WithImageTag(ZitadelContainerImageTags.Tag)
            .WithHttpEndpoint(port: port, targetPort: DefaultcontainerPort)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables[AdminEnvVarName] = resource.AdminReference;
                context.EnvironmentVariables[AdminEnvVarPassword] = resource.AdminPasswordParameter;
                // TODO: Implement Postgres/Cockroach DB integration. See https://zitadel.com/docs/self-hosting/deploy/compose
            });

        return zitadel;
    }
}
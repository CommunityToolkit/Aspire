// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Umami resources to the application model.
/// </summary>
public static class UmamiBuilderExtensions
{
    private const int UmamiPort = 3000;
    private const string SecretEnvVarName = "APP_SECRET";
    private const string DatabaseStorageEnvVarName = "DATABASE_URL";

    /// <summary>
    /// Adds a Umami resource to the application model. A container is used for local development.
    /// The default image is <inheritdoc cref="UmamiContainerImageTags.Image"/> and the tag is <inheritdoc cref="UmamiContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="secret">The parameter used to provide the app secret for the Umami resource.</param>
    /// <param name="port">The host port for the Umami app.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a Umami container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var db = builder.AddSurrealServer("surreal")
    ///   .AddNamespace("ns")
    ///   .AddDatabase("db");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<UmamiResource> AddUmami(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? secret = null,
        int? port = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // The secret must be at least 8 characters long and contain characters from three of the following four sets: Uppercase letters, Lowercase letters, Base 10 digits, and Symbols
        var secretParameter = secret?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-secret", minLower: 1, minUpper: 1, minNumeric: 1);

        var umami = new UmamiResource(name, secretParameter);

        return builder.AddResource(umami)
            .WithEndpoint(port: port, targetPort: UmamiPort, name: UmamiResource.PrimaryEndpointName)
            .WithImage(UmamiContainerImageTags.Image, UmamiContainerImageTags.Tag)
            .WithImageRegistry(UmamiContainerImageTags.Registry)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables[SecretEnvVarName] = umami.SecretParameter;
            })
            .WithHttpHealthCheck("/api/heartbeat");
    }

    /// <summary>
    /// References a <see cref="PostgresDatabaseResource"/> as the storage backend for the <see cref="UmamiResource"/>.
    /// </summary>
    /// <param name="builder">The Umami resource builder.</param>
    /// <param name="database">The PostgreSQL database resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<UmamiResource> WithStorageBackend(
        this IResourceBuilder<UmamiResource> builder, 
        IResourceBuilder<PostgresDatabaseResource> database
    )
    {
        builder.WaitFor(database);
        builder.WithEnvironment(async (context) =>
        {
            var connectionString = await database.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None).ConfigureAwait(false);
            if (connectionString is null)
            {
                throw new DistributedApplicationException($"Failed to retrieve the connection string of the '{database.Resource.Name}' resource.");
            }
            
            context.EnvironmentVariables[DatabaseStorageEnvVarName] = connectionString;
        });
        
        return builder;
    }
}
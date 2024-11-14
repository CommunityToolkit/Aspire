// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.SurrealDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SurrealDb.Net;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SurrealDB resources to the application model.
/// </summary>
public static class SurrealDbBuilderExtensions
{
    private const string UserEnvVarName = "SURREAL_USER";
    private const string PasswordEnvVarName = "SURREAL_PASS";

    /// <summary>
    /// Adds a SurrealDB resource to the application model. A container is used for local development.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the administrator username for the SurrealDB resource.</param>
    /// <param name="password">The parameter used to provide the administrator password for the SurrealDB resource. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="port">The host port for the SurrealDB instance.</param>
    /// <param name="strictMode">Whether strict mode is enabled on the server.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a SurrealDB container to the application model and reference it in a .NET project.
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
    public static IResourceBuilder<SurrealDbServerResource> AddSurrealServer(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        int? port = null,
        bool strictMode = false
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var args = new List<string>
        {
            "start"
        };
        if (strictMode)
        {
            args.Add("--strict");
        }

        // The password must be at least 8 characters long and contain characters from three of the following four sets: Uppercase letters, Lowercase letters, Base 10 digits, and Symbols
        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", minLower: 1, minUpper: 1, minNumeric: 1);

        var surrealServer = new SurrealDbServerResource(name, userName?.Resource, passwordParameter);
        return builder.AddResource(surrealServer)
                      .WithEndpoint(port: port, targetPort: 8000, name: SurrealDbServerResource.PrimaryEndpointName)
                      .WithImage(SurrealDbContainerImageTags.Image, SurrealDbContainerImageTags.Tag)
                      .WithImageRegistry(SurrealDbContainerImageTags.Registry)
                      .WithEnvironment(context =>
                      {
                          context.EnvironmentVariables[UserEnvVarName] = surrealServer.UserNameReference;
                          context.EnvironmentVariables[PasswordEnvVarName] = surrealServer.PasswordParameter;
                      })
                      .WithEntrypoint("/surreal")
                      .WithArgs([.. args]);
    }

    /// <summary>
    /// Adds a SurrealDB namespace to the application model. This is a child resource of a <see cref="SurrealDbServerResource"/>.
    /// </summary>
    /// <param name="builder">The SurrealDB resource builders.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="namespaceName">The name of the namespace. If not provided, this defaults to the same value as <paramref name="name"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a SurrealDB container to the application model and reference it in a .NET project.
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
    public static IResourceBuilder<SurrealDbNamespaceResource> AddNamespace(
        this IResourceBuilder<SurrealDbServerResource> builder, 
        [ResourceName] string name, 
        string? namespaceName = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Use the resource name as the namespace name if it's not provided
        namespaceName ??= name;

        builder.Resource.AddNamespace(name, namespaceName);
        var surrealServerNamespace = new SurrealDbNamespaceResource(name, namespaceName, builder.Resource);
        return builder.ApplicationBuilder.AddResource(surrealServerNamespace);
    }

    /// <summary>
    /// Adds a SurrealDB database to the application model. This is a child resource of a <see cref="SurrealDbNamespaceResource"/>.
    /// </summary>
    /// <param name="builder">The SurrealDB resource builders.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="databaseName">The name of the database. If not provided, this defaults to the same value as <paramref name="name"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a SurrealDB container to the application model and reference it in a .NET project.
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
    public static IResourceBuilder<SurrealDbDatabaseResource> AddDatabase(
        this IResourceBuilder<SurrealDbNamespaceResource> builder, 
        [ResourceName] string name, 
        string? databaseName = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Use the resource name as the database name if it's not provided
        databaseName ??= name;

        builder.Resource.AddDatabase(name, databaseName);
        var surrealServerDatabase = new SurrealDbDatabaseResource(name, databaseName, builder.Resource);

        SurrealDbClient? surrealDbClient = null;

        builder.ApplicationBuilder.Eventing.Subscribe<ConnectionStringAvailableEvent>(surrealServerDatabase, async (@event, ct) =>
        {
            var connectionString = await surrealServerDatabase.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false);
            if (connectionString == null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{surrealServerDatabase}' resource but the connection string was null.");
            }
            
            surrealDbClient = new SurrealDbClient(connectionString);
        });

        string namespaceName = builder.Resource.Name;
        string serverName = builder.Resource.Parent.Name;
        
        string healthCheckKey = $"{serverName}_{namespaceName}_{name}_check";
        builder.ApplicationBuilder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                healthCheckKey,
                sp => new SurrealDbHealthCheck(surrealDbClient!),
                failureStatus: default,
                tags: default,
                timeout: default)
            );
        
        return builder.ApplicationBuilder.AddResource(surrealServerDatabase)
            .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a SurrealDB resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an Meilisearch container to the application model and reference it in a .NET project.
    /// Additionally, in this example a data volume is added to the container
    /// to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var db = builder.AddSurrealServer("surreal")
    ///   .WithDataVolume()
    ///   .AddNamespace("ns")
    ///   .AddDatabase("db");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<SurrealDbServerResource> WithDataVolume(this IResourceBuilder<SurrealDbServerResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

#pragma warning disable CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "data"), "/var/opt/surreal");
#pragma warning restore CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a SurrealDB resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a SurrealDB container to the application model and reference it in a .NET project.
    /// Additionally, in this example a bind mount is added to the container
    /// to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var db = builder.AddSurrealServer("surreal")
    ///   .WithDataBindMount("./data/surreal/data")
    ///   .AddNamespace("ns")
    ///   .AddDatabase("db");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<SurrealDbServerResource> WithDataBindMount(this IResourceBuilder<SurrealDbServerResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/var/opt/surreal");
    }
}
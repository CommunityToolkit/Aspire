// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.SurrealDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SurrealDb.Net;
using System.Text.Json;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding SurrealDB resources to the application model.
/// </summary>
public static class SurrealDbBuilderExtensions
{
    private const int SurrealDbPort = 8000;
    private const string UserEnvVarName = "SURREAL_USER";
    private const string PasswordEnvVarName = "SURREAL_PASS";

    /// <summary>
    /// Adds a SurrealDB resource to the application model. A container is used for local development.
    /// The default image is <inheritdoc cref="SurrealDbContainerImageTags.Image"/> and the tag is <inheritdoc cref="SurrealDbContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the administrator username for the SurrealDB resource.</param>
    /// <param name="password">The parameter used to provide the administrator password for the SurrealDB resource. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="port">The host port for the SurrealDB instance.</param>
    /// <param name="path">Sets the path for storing data. If no argument is given, the default of <c>memory</c> for non-persistent storage in memory is assumed.</param>
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
        string path = "memory",
        bool strictMode = false
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var args = new List<string>
        {
            "start",
            path
        };
        if (strictMode)
        {
            args.Add("--strict");
        }

        // The password must be at least 8 characters long and contain characters from three of the following four sets: Uppercase letters, Lowercase letters, Base 10 digits, and Symbols
        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password", minLower: 1, minUpper: 1, minNumeric: 1);

        var surrealServer = new SurrealDbServerResource(name, userName?.Resource, passwordParameter);
        return builder.AddResource(surrealServer)
                      .WithEndpoint(port: port, targetPort: SurrealDbPort, name: SurrealDbServerResource.PrimaryEndpointName)
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
            if (connectionString is null)
            {
                throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{surrealServerDatabase}' resource but the connection string was null.");
            }
            
            var options = new SurrealDbOptionsBuilder().FromConnectionString(connectionString).Build();
            surrealDbClient = new SurrealDbClient(options);
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
    /// Add a SurrealDB container to the application model and reference it in a .NET project.
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

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/opt/surreal");
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
    
    /// <summary>
    /// Adds a Surrealist UI instance for SurrealDB to the application model.
    /// The default image is <inheritdoc cref="SurrealDbContainerImageTags.SurrealistImage"/> and the tag is <inheritdoc cref="SurrealDbContainerImageTags.SurrealistTag"/>.
    /// </summary>
    /// <param name="builder">The SurrealDB server resource builder.</param>
    /// <param name="configureContainer">Callback to configure Surrealist container resource.</param>
    /// <param name="containerName">The name of the container (optional).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<T> WithSurrealist<T>(
        this IResourceBuilder<T> builder,
        Action<IResourceBuilder<SurrealistContainerResource>>? configureContainer = null,
        string? containerName = null
    )
        where T : SurrealDbServerResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        
        if (builder.ApplicationBuilder.Resources.OfType<SurrealistContainerResource>().SingleOrDefault() is { } existingSurrealistResource)
        {
            var builderForExistingResource = builder.ApplicationBuilder.CreateResourceBuilder(existingSurrealistResource);
            configureContainer?.Invoke(builderForExistingResource);
            
            return builder;
        }
        
        containerName ??= $"{builder.Resource.Name}-surrealist";
        
        const string CONNECTIONS_FILE_PATH = "/usr/share/nginx/html/connections.json";
        
        var surrealistContainer = new SurrealistContainerResource(containerName);
        var surrealistContainerBuilder = builder.ApplicationBuilder.AddResource(surrealistContainer)
            .WithImage(SurrealDbContainerImageTags.SurrealistImage, SurrealDbContainerImageTags.SurrealistTag)
            .WithImageRegistry(SurrealDbContainerImageTags.SurrealistRegistry)
            .WithHttpEndpoint(targetPort: 8080, name: "http")
            .WithBindMount(Path.GetTempFileName(), CONNECTIONS_FILE_PATH)
            .WithRelationship(builder.Resource, "Surrealist")
            .ExcludeFromManifest();
        
        builder.ApplicationBuilder.Eventing.Subscribe<AfterEndpointsAllocatedEvent>( (e, ct) =>
            {
                var serverFileMount = surrealistContainer.Annotations.OfType<ContainerMountAnnotation>().Single(v => v.Target == CONNECTIONS_FILE_PATH);
                var surrealDbServerResources = builder.ApplicationBuilder.Resources.OfType<SurrealDbServerResource>().ToList();

                using var stream = new FileStream(serverFileMount.Source!, FileMode.Create);
                using var writer = new Utf8JsonWriter(stream);
                
                // Need to grant read access to the config file on unix like systems.
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(serverFileMount.Source!, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
                }

                writer.WriteStartObject();

                if (surrealDbServerResources.Count == 1)
                {
                    var uniqueSurrealDbResource = surrealDbServerResources[0];
                    writer.WriteString("defaultConnection", uniqueSurrealDbResource.Name);
                }

                writer.WriteStartArray("connections");
                
                var surrealDbNamespaceResources = builder.ApplicationBuilder.Resources.OfType<SurrealDbNamespaceResource>().ToList();
                var surrealDbDatabaseResources = builder.ApplicationBuilder.Resources.OfType<SurrealDbDatabaseResource>().ToList();

                foreach (var surrealInstance in surrealDbServerResources)
                {
                    if (surrealInstance.PrimaryEndpoint.IsAllocated)
                    {
                        SurrealDbNamespaceResource? uniqueNamespace = null;
                        SurrealDbDatabaseResource? uniqueDatabase = null;
                        
                        var serverNamespaces = surrealDbNamespaceResources
                            .Where(ns => ns.Parent == surrealInstance)
                            .ToList();

                        if (serverNamespaces.Count == 1)
                        {
                            uniqueNamespace = serverNamespaces.First();
                            
                            var nsDatabases = surrealDbDatabaseResources
                                .Where(db => db.Parent == uniqueNamespace)
                                .ToList();
                            
                            if (nsDatabases.Count == 1)
                            {
                                uniqueDatabase = nsDatabases.First();
                            }
                        }
                        
                        var endpoint = surrealInstance.PrimaryEndpoint;

                        writer.WriteStartObject();

                        writer.WriteString("id", surrealInstance.Name);
                        writer.WriteString("name", surrealInstance.Name);
                        
                        if (uniqueNamespace is not null)
                        {
                            writer.WriteString("defaultNamespace", uniqueNamespace.NamespaceName);
                        }
                        if (uniqueDatabase is not null)
                        {
                            writer.WriteString("defaultDatabase", uniqueDatabase.DatabaseName);
                        }
                        
                        writer.WriteStartObject("authentication");
                        writer.WriteString("protocol", "ws");
                        // How to do host resolution?
                        writer.WriteString("hostname", $"localhost:{endpoint.Port}");
                        writer.WriteString("mode", "root");
                        if (uniqueNamespace is not null)
                        {
                            writer.WriteString("namespace", uniqueNamespace.NamespaceName);
                        }
                        if (uniqueDatabase is not null)
                        {
                            writer.WriteString("database", uniqueDatabase.DatabaseName);
                        }
                        
                        writer.WriteEndObject();
                        
                        writer.WriteEndObject();
                    }
                }

                writer.WriteEndArray();
                
                writer.WriteEndObject();
                
                return Task.CompletedTask;
            });
        
        configureContainer?.Invoke(surrealistContainerBuilder);

        return builder;
    }
}
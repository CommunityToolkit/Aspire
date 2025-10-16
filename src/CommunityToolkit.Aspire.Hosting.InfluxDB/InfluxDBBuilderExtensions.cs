using System.Data.Common;
using CommunityToolkit.Aspire.Hosting.InfluxDB;
using Aspire.Hosting.ApplicationModel;
using InfluxDB.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using HealthChecks.InfluxDB;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding InfluxDB resources to the application model.
/// </summary>
public static class InfluxDBBuilderExtensions
{
    private const int InfluxDBPort = 8086;

    /// <summary>
    /// Adds an InfluxDB container resource to the application model.
    /// The default image is <inheritdoc cref="InfluxDBContainerImageTags.Image"/> and the tag is <inheritdoc cref="InfluxDBContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the username for the InfluxDB. If <see langword="null"/> a default value will be used.</param>
    /// <param name="password">The parameter used to provide the password for the InfluxDB. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="token">The parameter used to provide the admin token for the InfluxDB. If <see langword="null"/> a random token will be generated.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an InfluxDB container to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var influxdb = builder.AddInfluxDB("influxdb");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(influxdb);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<InfluxDBResource> AddInfluxDB(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        IResourceBuilder<ParameterResource>? token = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var userNameParameter = userName?.Resource;
        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");
        var tokenParameter = token?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-token");

        var influxdb = new InfluxDBResource(name, userNameParameter, passwordParameter, tokenParameter);

        InfluxDBClient? influxdbClient = null;

        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(influxdb, async (@event, ct) =>
        {
            var connectionString = await influxdb.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false)
            ?? throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{influxdb.Name}' resource but the connection string was null.");

            influxdbClient = CreateInfluxDBClient(connectionString);
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
         .Add(new HealthCheckRegistration(
             healthCheckKey,
             sp => new InfluxDBHealthCheck(influxdbClient!),
             failureStatus: default,
             tags: default,
             timeout: default));

        return builder.AddResource(influxdb)
             .WithImage(InfluxDBContainerImageTags.Image, InfluxDBContainerImageTags.Tag)
             .WithImageRegistry(InfluxDBContainerImageTags.Registry)
             .WithHttpEndpoint(targetPort: InfluxDBPort, port: port, name: InfluxDBResource.PrimaryEndpointName)
             .WithEnvironment(context =>
             {
                 context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_MODE"] = "setup";
                 context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_USERNAME"] = influxdb.UserNameReference;
                 context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_PASSWORD"] = influxdb.PasswordParameter;
                 context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_ORG"] = "default";
                 context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_BUCKET"] = "default";
                 context.EnvironmentVariables["DOCKER_INFLUXDB_INIT_ADMIN_TOKEN"] = influxdb.TokenParameter;
             })
             .WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a named volume for the data folder to an InfluxDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an InfluxDB container to the application model and reference it in a .NET project. Additionally, in this
    /// example a data volume is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var influxdb = builder.AddInfluxDB("influxdb")
    /// .WithDataVolume();
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(influxdb);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<InfluxDBResource> WithDataVolume(this IResourceBuilder<InfluxDBResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/influxdb2");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to an InfluxDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add an InfluxDB container to the application model and reference it in a .NET project. Additionally, in this
    /// example a bind mount is added to the container to allow data to be persisted across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var influxdb = builder.AddInfluxDB("influxdb")
    /// .WithDataBindMount("./data/influxdb/data");
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(influxdb);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<InfluxDBResource> WithDataBindMount(this IResourceBuilder<InfluxDBResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/lib/influxdb2");
    }

    internal static InfluxDBClient CreateInfluxDBClient(string? connectionString)
    {
        if (connectionString is null)
        {
            throw new InvalidOperationException("Connection string is unavailable");
        }

        Uri? url = null;
        string? token = null;
        string? organization = null;
        string? bucket = null;

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            url = uri;
        }
        else
        {
            var connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.TryGetValue("Url", out var urlValue) && Uri.TryCreate(urlValue.ToString(), UriKind.Absolute, out var serviceUri))
            {
                url = serviceUri;
            }

            if (connectionBuilder.TryGetValue("Token", out var tokenValue))
            {
                token = tokenValue.ToString();
            }

            if (connectionBuilder.TryGetValue("Organization", out var organizationValue))
            {
                organization = organizationValue.ToString();
            }

            if (connectionBuilder.TryGetValue("Bucket", out var bucketValue))
            {
                bucket = bucketValue.ToString();
            }
        }

        var options = new InfluxDBClientOptions(url!.ToString())
        {
            Token = token!,
            Org = organization,
            Bucket = bucket
        };

        return new InfluxDBClient(options);
    }
}

using CommunityToolkit.Aspire.Hosting.Neon;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Neon resources to the application model.
/// </summary>
public static class NeonBuilderExtensions
{
    private const int NeonPort = 5432;

    /// <summary>
    /// Adds a Neon project resource to the application model.
    /// The default image is <inheritdoc cref="NeonContainerImageTags.Image"/> and the tag is <inheritdoc cref="NeonContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="userName">The parameter used to provide the user name for the Neon project. If <see langword="null"/> a default user name will be used.</param>
    /// <param name="password">The parameter used to provide the password for the Neon project. If <see langword="null"/> a random password will be generated.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a Neon project to the application model and reference it in a .NET project.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var neon = builder.AddNeonProject("neon");
    /// var db = neon.AddDatabase("neondb");
    /// 
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<NeonProjectResource> AddNeonProject(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource>? userName = null,
        IResourceBuilder<ParameterResource>? password = null,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var userNameParameter = userName?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-username", special: false);
        var passwordParameter = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");

        var neonProject = new NeonProjectResource(name, userNameParameter, passwordParameter);

        return builder.AddResource(neonProject)
             .WithImage(NeonContainerImageTags.Image, NeonContainerImageTags.Tag)
             .WithImageRegistry(NeonContainerImageTags.Registry)
             .WithEndpoint(targetPort: NeonPort, port: port, name: NeonProjectResource.PrimaryEndpointName)
             .WithEnvironment(context =>
             {
                 context.EnvironmentVariables["POSTGRES_USER"] = neonProject.UserNameParameter;
                 context.EnvironmentVariables["POSTGRES_PASSWORD"] = neonProject.PasswordParameter;
             });
    }

    /// <summary>
    /// Adds a Neon database resource to the application model.
    /// </summary>
    /// <param name="builder">The Neon project resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="databaseName">The name of the database. If not provided, this defaults to the same value as <paramref name="name"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a Neon project with a database to the application model.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var neon = builder.AddNeonProject("neon")
    ///   .AddDatabase("neondb");
    ///
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(neon);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<NeonDatabaseResource> AddDatabase(
        this IResourceBuilder<NeonProjectResource> builder,
        [ResourceName] string name,
        string? databaseName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var databaseResource = new NeonDatabaseResource(name, databaseName ?? name, builder.Resource);

        return builder.ApplicationBuilder.AddResource(databaseResource);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a Neon project resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a Neon project to the application model with a data volume to persist data across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var neon = builder.AddNeonProject("neon")
    ///   .WithDataVolume();
    /// var db = neon.AddDatabase("neondb");
    /// 
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<NeonProjectResource> WithDataVolume(this IResourceBuilder<NeonProjectResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/postgresql/data");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Neon project resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <example>
    /// Add a Neon project to the application model with a data bind mount to persist data across container restarts.
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var neon = builder.AddNeonProject("neon")
    ///   .WithDataBindMount("./data/neon");
    /// var db = neon.AddDatabase("neondb");
    /// 
    /// var api = builder.AddProject&lt;Projects.Api&gt;("api")
    ///   .WithReference(db);
    ///  
    /// builder.Build().Run(); 
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<NeonProjectResource> WithDataBindMount(this IResourceBuilder<NeonProjectResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/lib/postgresql/data");
    }
}

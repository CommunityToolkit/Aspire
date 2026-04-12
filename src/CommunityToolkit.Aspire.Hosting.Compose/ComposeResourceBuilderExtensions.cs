using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Compose;
using CommunityToolkit.Aspire.Hosting.Compose.Mapping;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing;
using CommunityToolkit.Aspire.Hosting.Compose.Parsing.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

#pragma warning disable ASPIREATS001

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Docker Compose files to the Aspire application model.
/// </summary>
public static class ComposeResourceBuilderExtensions
{
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds a Docker Compose file using a source-generated type.
        /// Returns a typed wrapper with properties for each compose service.
        /// </summary>
        /// <typeparam name="TCompose">
        /// A source-generated class from the <c>Compose</c> namespace (e.g., <c>Compose.Infra</c>).
        /// </typeparam>
        /// <returns>A typed wrapper with properties for each service (e.g., <c>infra.Postgres</c>).</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the type does not have a <c>ComposeReferencePathAttribute</c> or valid constructor.
        /// </exception>
        /// <example>
        /// <code>
        /// var infra = builder.AddCompose&lt;Compose.Infra&gt;();
        ///
        /// builder.AddProject&lt;Projects.MyApi&gt;("api")
        ///     .WaitFor(infra.Postgres)
        ///     .WaitFor(infra.Redis);
        /// </code>
        /// </example>
        [AspireExport("addComposeTyped", Description = "Adds a Docker Compose file using a source-generated type")]
        public TCompose AddCompose<TCompose>() where TCompose : class
        {
            ArgumentNullException.ThrowIfNull(builder);
            Type composeType = typeof(TCompose);
            ComposeReferencePathAttribute pathAttr = composeType.GetCustomAttribute<ComposeReferencePathAttribute>()
                ?? throw new InvalidOperationException($"Type '{composeType.FullName}' does not have a ComposeReferencePathAttribute. " + "Ensure it was generated from a <ComposeReference> MSBuild item.");
            ComposeResourceCollection collection = builder.AddCompose(pathAttr.Path);

            return Activator.CreateInstance(composeType, collection) as TCompose ?? throw new InvalidOperationException( $"Failed to create instance of '{composeType.FullName}'. " + $"Ensure it has a public constructor accepting {nameof(ComposeResourceCollection)}.");
        }

        /// <summary>
        /// Adds a Docker Compose file to the Aspire application model.
        /// Each service defined in the compose file becomes a container resource.
        /// </summary>
        /// <param name="composePath">The path to the Docker Compose file (absolute or relative to the AppHost project directory).</param>
        /// <returns>A <see cref="ComposeResourceCollection"/> providing access to the created resources.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the compose file does not exist.</exception>
        /// <exception cref="ComposeParseException">Thrown when the YAML content is invalid.</exception>
        /// <example>
        /// <code>
        /// var infra = builder.AddCompose(".infra/compose.yml");
        ///
        /// builder.AddProject&lt;Projects.MyApi&gt;("api")
        ///     .WaitFor(infra["postgres"])
        ///     .WaitFor(infra["redis"]);
        /// </code>
        /// </example>
        [AspireExport("addCompose", Description = "Adds a Docker Compose file to the Aspire application model")]
        public ComposeResourceCollection AddCompose(string composePath)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrEmpty(composePath);
            ILoggerFactory? loggerFactory = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>();
            ILogger? logger = loggerFactory?.CreateLogger("CommunityToolkit.Aspire.Hosting.Compose");
            string resolvedPath = ResolveComposePath(builder, composePath);
            ComposeFile compose = ComposeParser.Parse(resolvedPath);
            Dictionary<string, IResourceBuilder<ContainerResource>> resources = ServiceToResourceMapper.MapServices(builder, compose, resolvedPath, logger);

            return new ComposeResourceCollection(resolvedPath, resources);
        }

        /// <summary>
        /// Adds multiple Docker Compose files to the Aspire application model.
        /// </summary>
        /// <param name="composePaths">The paths to the Docker Compose files.</param>
        /// <returns>An array of <see cref="ComposeResourceCollection"/> instances, one per file.</returns>
        public ComposeResourceCollection[] AddCompose(params string[] composePaths)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(composePaths);
            
            return [.. composePaths.Select(builder.AddCompose)];
        }
    }

    private static string ResolveComposePath(IDistributedApplicationBuilder builder, string composePath) =>
        Path.IsPathRooted(composePath)
            ? composePath
            : Path.GetFullPath(Path.Combine(builder.AppHostDirectory, composePath));
}

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for referencing <see cref="NeonDatabaseResource"/> instances
/// from consumer resources. In publish mode the standard connection-string injection
/// is replaced by a shared Docker volume and an entrypoint wrapper that sources the
/// per-database <c>.env</c> file written by the Neon provisioner container.
/// </summary>
public static class NeonReferenceExtensions
{
    /// <summary>
    /// The container mount path where Neon provisioner output is made available to consumers.
    /// </summary>
    private const string NeonOutputMountPath = "/neon-output";

    /// <summary>
    /// Configures a resource to consume a Neon database, making the connection string
    /// available as <c>ConnectionStrings__{connectionName}</c>.
    /// </summary>
    /// <typeparam name="TDestination">The type of the consuming resource.</typeparam>
    /// <param name="builder">The resource builder for the consumer.</param>
    /// <param name="source">The Neon database resource to reference.</param>
    /// <param name="connectionName">
    /// The connection string name. Defaults to the database resource name.
    /// </param>
    /// <param name="optional">
    /// Whether the connection string is optional. Only used in run mode
    /// where the standard <c>WithReference</c> path is taken.
    /// </param>
    /// <returns>The consumer resource builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Run mode:</strong> delegates to the standard Aspire
    /// <c>WithReference</c> method. The Neon provisioner event handler
    /// populates <see cref="NeonDatabaseResource.ConnectionUri"/> at
    /// runtime, so the connection string is available as normal.
    /// </para>
    /// <para>
    /// <strong>Publish mode (container consumers):</strong> the provisioner's
    /// output volume is mounted into the consumer container and the
    /// entrypoint is replaced with a thin shell wrapper that:
    /// </para>
    /// <list type="number">
    /// <item><description>Waits for the per-database <c>.env</c> file to appear.</description></item>
    /// <item><description>Sources it to obtain <c>NEON_CONNECTION_URI</c>.</description></item>
    /// <item><description>Exports <c>ConnectionStrings__{name}</c> from that value.</description></item>
    /// <item><description>Execs the original container command (<c>$@</c>).</description></item>
    /// </list>
    /// <para>
    /// <strong>Publish mode (project / non-container consumers):</strong>
    /// the volume is mounted and helper environment variables
    /// (<c>NEON_OUTPUT_DIR</c>, <c>NEON_ENV_FILE__{name}</c>) are injected so
    /// the consumer application can read the <c>.env</c> file at startup.
    /// Use <c>AddNeonClient</c> or <c>AddNeonConnectionStrings</c> in the
    /// consumer application to resolve these automatically.
    /// </para>
    /// <example>
    /// <code lang="csharp">
    /// var neon = builder.AddNeon("neon", apiKey).AddProject("my-project");
    /// var db = neon.AddDatabase("appdb");
    ///
    /// // .NET project consumer:
    /// builder.AddProject&lt;Projects.Api&gt;("api")
    ///     .WithReference(db);
    ///
    /// // Container consumer (e.g., a custom container):
    /// builder.AddContainer("worker", "my-worker-image")
    ///     .WithReference(db);
    /// </code>
    /// </example>
    /// </remarks>
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<NeonDatabaseResource> source,
        string? connectionName = null,
        bool optional = false)
        where TDestination : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        connectionName ??= source.Resource.Name;

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder.WithReference(
                (IResourceBuilder<IResourceWithConnectionString>)source,
                connectionName,
                optional);
        }

        NeonProjectResource neonProject = source.Resource.Parent;
        string envFilePath = $"{NeonOutputMountPath}/{source.Resource.Name}.env";

        if (neonProject.OutputVolumeName is not null)
        {
            builder.WithAnnotation(new ContainerMountAnnotation(
                neonProject.OutputVolumeName,
                NeonOutputMountPath,
                ContainerMountType.Volume,
                isReadOnly: true));
        }

        if (neonProject.ProvisionerResource is ProjectResource provisioner
            && builder.Resource is IResourceWithWaitSupport)
        {
            var waitBuilder = builder.ApplicationBuilder
                .CreateResourceBuilder((IResourceWithWaitSupport)builder.Resource);
            waitBuilder.WaitForCompletion(
                builder.ApplicationBuilder.CreateResourceBuilder(provisioner));
        }

        string connStringEnvVar = $"ConnectionStrings__{connectionName}";

        if (builder.Resource is ContainerResource container)
        {
            string startupScript =
                $"while [ ! -f {envFilePath} ]; do echo 'Waiting for Neon provisioner output...'; sleep 2; done; " +
                $". {envFilePath}; " +
                $"export {connStringEnvVar}=\"$NEON_CONNECTION_URI\"; " +
                "exec \"$@\"";

            container.Entrypoint = "/bin/sh";

            var containerBuilder = builder.ApplicationBuilder
                .CreateResourceBuilder(container);
            containerBuilder.WithArgs(ctx =>
            {
                var existingArgs = ctx.Args.ToList();
                ctx.Args.Clear();
                ctx.Args.Add("-c");
                ctx.Args.Add(startupScript);
                ctx.Args.Add("_"); // placeholder for $0 in sh -c
                foreach (var arg in existingArgs)
                {
                    ctx.Args.Add(arg);
                }
            });
        }
        else
        {
            builder.WithEnvironment("NEON_OUTPUT_DIR", NeonOutputMountPath);
            builder.WithEnvironment($"NEON_ENV_FILE__{connectionName}", envFilePath);
        }

        if (builder.Resource is IResourceWithWaitSupport)
        {
            var waitBuilder = builder.ApplicationBuilder
                .CreateResourceBuilder((IResourceWithWaitSupport)builder.Resource);
            waitBuilder.WaitFor(
                builder.ApplicationBuilder.CreateResourceBuilder(source.Resource));
        }

        return builder;
    }
}

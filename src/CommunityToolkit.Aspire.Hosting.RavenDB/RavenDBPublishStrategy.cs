using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel
{
    /// <summary>
    /// How a deployment package, such as the RavenDB Cloud one, publishes a RavenDB server instead of its container.
    /// </summary>
    /// <param name="Name">The method that chose the strategy, as error messages name it, such as <c>PublishAsRavenDBCloud</c>.</param>
    /// <param name="Url">Where the applications connect once deployed; it may only be known at deploy time.</param>
    [Experimental("CTASPIRERAVENDB001")]
    public sealed record RavenDBPublishStrategy(string Name, ReferenceExpression Url);
}

namespace Aspire.Hosting
{
    public static partial class RavenDBBuilderExtensions
    {
        /// <summary>
        /// For deployment packages: publishes the server with <paramref name="strategy"/> instead of its container. The
        /// container is left out of the published artifacts, and the applications that reference the server connect to
        /// the strategy's URL. A server has one strategy. AppHosts call the deployment package's own method, such as
        /// <c>PublishAsRavenDBCloud</c>.
        /// </summary>
        /// <param name="builder">The resource builder for the RavenDB server.</param>
        /// <param name="strategy">The strategy.</param>
        /// <returns>The <see cref="IResourceBuilder{T}"/> for the RavenDB server resource.</returns>
        [Experimental("CTASPIRERAVENDB001")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [AspireExportIgnore(Reason = "Deployment packages call it; AppHosts call their own publish methods, such as PublishAsRavenDBCloud.")]
        public static IResourceBuilder<RavenDBServerResource> WithPublishStrategy(
            this IResourceBuilder<RavenDBServerResource> builder,
            RavenDBPublishStrategy strategy)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(strategy);

            builder.Resource.UsePublishStrategy(strategy);
            builder.ExcludeFromManifest();

            // WaitFor(server) waits for the local container. Published, there is no container to wait for, and an
            // environment such as Docker Compose rejects a dependency on a service it does not have.
            builder.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((@event, _) =>
            {
                foreach (var resource in @event.Model.Resources)
                {
                    var waits = resource.Annotations
                        .OfType<WaitAnnotation>()
                        .Where(wait => IsServerOrItsDatabase(wait.Resource, builder.Resource))
                        .ToList();

                    foreach (var wait in waits)
                    {
                        resource.Annotations.Remove(wait);
                    }
                }

                return Task.CompletedTask;
            });

            return builder;
        }

        private static bool IsServerOrItsDatabase(IResource resource, RavenDBServerResource server) =>
            ReferenceEquals(resource, server) ||
            (resource is RavenDBDatabaseResource database && ReferenceEquals(database.Parent, server));
    }
}

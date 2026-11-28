// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker;
using Aspire.Hosting.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;

namespace Aspire.Hosting;

public static partial class FlareBuilderExtensions
{
    // Flare's query API finds the containers to draw by these labels. flare.relationships lists the
    // roles a container depends on, as role:Reference pairs.
    private const string ResourceLabel = "flare.resource";
    private const string RoleLabel = "flare.role";
    private const string RelationshipsLabel = "flare.relationships";
    private const string StorageRelationships = "clickhouse:Reference,redis:Reference";

    private const int DockerProxyPort = 2375;

    /// <summary>
    /// Turns on the Resources page of the Flare dashboard, a live graph of the Flare containers and the
    /// ClickHouse and Redis resources they use. The query API reads the graph from the container runtime:
    /// from Docker through a read-only socket proxy, or, when the app is published to Kubernetes, from the
    /// Kubernetes API with a read-only service account.
    /// </summary>
    /// <ats-summary>Turns on the Resources page of the Flare dashboard</ats-summary>
    /// <param name="builder">The Flare resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// <para>
    /// The Flare containers, ClickHouse and Redis get <c>flare.resource</c>, <c>flare.role</c> and
    /// <c>flare.relationships</c> labels, both locally and in Docker Compose and Kubernetes output.
    /// </para>
    /// <para>
    /// Locally and with Docker Compose, a <c>tecnativa/docker-socket-proxy</c> container is added with the
    /// Docker socket mounted read-only. It allows only listing and inspecting containers, and the query API
    /// talks to the proxy, never to the socket.
    /// </para>
    /// <para>
    /// When the app is published to a Kubernetes environment, no proxy is added. Instead the query API runs
    /// under its own service account, with a role that can only get, list and watch pods and services in its
    /// namespace.
    /// </para>
    /// </remarks>
    [AspireExport]
    public static IResourceBuilder<FlareResource> WithResourceGraph(this IResourceBuilder<FlareResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var resource = builder.Resource;
        var applicationBuilder = builder.ApplicationBuilder;
        var api = applicationBuilder.CreateResourceBuilder(resource.Api);

        FlareLabels[] labels =
        [
            new(resource, "ingest", StorageRelationships),
            new(resource.Api, "api", StorageRelationships),
            new(resource.AlertWorker, "alert-worker", StorageRelationships),
            new(resource.Dashboard, "dashboard", "api:Reference"),
            new(resource.ClickHouse, "clickhouse", null),
            new(resource.Redis, "redis", null),
        ];

        foreach (var label in labels)
        {
            applicationBuilder.CreateResourceBuilder(label.Resource).WithContainerRuntimeArgs([.. label.RuntimeArgs()]);
        }

        var proxy = applicationBuilder.AddContainer($"{resource.Name}-docker-proxy", FlareContainerImageTags.DockerProxyImage, FlareContainerImageTags.DockerProxyTag)
            .WithImageRegistry(FlareContainerImageTags.Registry)
            .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock", isReadOnly: true)
            .WithEnvironment("CONTAINERS", "1")
            .WithEnvironment("POST", "0")
            .WithHttpEndpoint(targetPort: DockerProxyPort, name: "http")
            .WithParentRelationship(builder)
            .WithIconName("PlugConnected");

        // Decided when the environment is read rather than now, so a Kubernetes environment added after this
        // call still counts. `aspire run` always runs on Docker, even with a Kubernetes environment registered.
        api.WithEnvironment(context =>
        {
            if (TargetsKubernetes(applicationBuilder))
            {
                context.EnvironmentVariables["KubernetesResources__Enabled"] = "true";
            }
            else
            {
                context.EnvironmentVariables["DockerResources__ProxyUrl"] = proxy.GetEndpoint("http");
            }
        });

        if (applicationBuilder.ExecutionContext.IsPublishMode)
        {
            // The publishers reject Compose or Kubernetes settings on a resource when that environment is
            // missing, so they are only added once the whole app model, environments included, is built.
            applicationBuilder.Eventing.Subscribe<BeforeStartEvent>((@event, _) =>
            {
                var model = @event.Model.Resources;

                if (model.OfType<DockerComposeEnvironmentResource>().Any())
                {
                    foreach (var label in labels)
                    {
                        applicationBuilder.CreateResourceBuilder(label.Resource).PublishAsDockerComposeService((_, service) => label.Apply(service));
                    }
                }

                if (model.OfType<KubernetesEnvironmentResource>().Any())
                {
                    foreach (var label in labels)
                    {
                        applicationBuilder.CreateResourceBuilder(label.Resource).PublishAsKubernetesService(label.Apply);
                    }

                    api.PublishAsKubernetesService(service => AddResourceGraphRbac(service, resource.Name));

                    // A Docker socket has no meaning on a Kubernetes node, so the proxy is left out there.
                    proxy.ExcludeFromManifest();
                }

                return Task.CompletedTask;
            });
        }

        return builder;
    }

    private static bool TargetsKubernetes(IDistributedApplicationBuilder builder) =>
        builder.ExecutionContext.IsPublishMode && builder.Resources.OfType<KubernetesEnvironmentResource>().Any();

    private static void AddResourceGraphRbac(KubernetesResource service, string name)
    {
        var serviceAccount = new ServiceAccountV1();
        serviceAccount.Metadata.Name = $"{name}-resource-graph";

        var role = new Role();
        role.Metadata.Name = $"{name}-resource-graph";
        role.Rules.Add(new PolicyRuleV1
        {
            ApiGroups = { "" },
            Resources = { "pods", "services" },
            Verbs = { "get", "list", "watch" },
        });

        var roleBinding = new RoleBinding();
        roleBinding.Metadata.Name = $"{name}-resource-graph";
        roleBinding.RoleRef = new RoleRefV1
        {
            ApiGroup = "rbac.authorization.k8s.io",
            Kind = "Role",
            Name = role.Metadata.Name,
        };
        roleBinding.Subjects.Add(new SubjectV1
        {
            Kind = "ServiceAccount",
            Name = serviceAccount.Metadata.Name,
            // A service account subject needs its namespace; the release's namespace is where all of
            // these objects are installed.
            Namespace = "{{ .Release.Namespace }}",
        });

        service.AdditionalResources.Add(serviceAccount);
        service.AdditionalResources.Add(role);
        service.AdditionalResources.Add(roleBinding);

        if (service.Workload is { } workload)
        {
            workload.PodTemplate.Spec.ServiceAccountName = serviceAccount.Metadata.Name;
        }
    }

    // The labels one container carries in the graph: its role, and the roles it depends on.
    private sealed record FlareLabels(ContainerResource Resource, string Role, string? Relationships)
    {
        public IEnumerable<string> RuntimeArgs()
        {
            yield return "--label";
            yield return $"{ResourceLabel}=true";
            yield return "--label";
            yield return $"{RoleLabel}={Role}";

            if (Relationships is not null)
            {
                yield return "--label";
                yield return $"{RelationshipsLabel}={Relationships}";
            }
        }

        public void Apply(Docker.Resources.ComposeNodes.Service service)
        {
            service.Labels[ResourceLabel] = "true";
            service.Labels[RoleLabel] = Role;

            if (Relationships is not null)
            {
                service.Labels[RelationshipsLabel] = Relationships;
            }
        }

        public void Apply(KubernetesResource service)
        {
            if (service.Workload is not { } workload)
            {
                return;
            }

            workload.PodTemplate.Metadata.Labels[ResourceLabel] = "true";
            workload.PodTemplate.Metadata.Labels[RoleLabel] = Role;

            // Label values can't contain ':' or ',', so the relationships go in an annotation on Kubernetes.
            if (Relationships is not null)
            {
                workload.PodTemplate.Metadata.Annotations[RelationshipsLabel] = Relationships;
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Management;

#pragma warning disable ASPIREPROBES001
namespace CommunityToolkit.Aspire.Hosting.GlitchTip;

internal static class GlitchTipMonitoring
{
    internal static void ConfigureLocalReferences(IResourceBuilder<GlitchTipResource> builder)
    {
        builder.ApplicationBuilder.Eventing.Subscribe<BeforeStartEvent>((_, _) =>
        {
            var server = builder.ApplicationBuilder.CreateResourceBuilder(builder.Resource.LocalServer!);
            foreach (var consumer in builder.ApplicationBuilder.Resources.Where(r => r.Annotations.OfType<GlitchTipConsumerAnnotation>().Any(a => a.Project == builder.Resource)))
            {
                foreach (var check in GetChecks(consumer, builder.Resource).Where(c => c.Enabled))
                {
                    // Declare the reverse reference so Aspire allocates its container-to-host
                    // tunnel before resolving monitor URLs. This adds no readiness dependency.
                    foreach (var endpoint in GetReferencedEndpoints(check.Url is null ? check.Endpoint : check.Url))
                    {
                        server.WithReference(endpoint);
                    }
                }
            }
            return Task.CompletedTask;
        });
    }

    private static IEnumerable<EndpointReference> GetReferencedEndpoints(object value)
    {
        var pending = new Stack<object>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        pending.Push(value);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current)) continue;
            if (current is EndpointReference endpoint)
            {
                yield return endpoint;
            }
            else if (current is IValueWithReferences references)
            {
                foreach (var reference in references.References) pending.Push(reference);
            }
        }
    }
    internal static List<GlitchTipMonitorAnnotation> GetChecks(IResource consumer, GlitchTipResource resource)
    {
        var checks = consumer.Annotations.OfType<GlitchTipMonitorAnnotation>().ToList();
        // HTTP probes retain structured metadata in Aspire. Ordinary WithHttpHealthCheck does not in 13.5.
        foreach (var probe in consumer.Annotations.OfType<EndpointProbeAnnotation>())
        {
            var identity = $"{probe.EndpointReference.EndpointName}:{probe.Path}";
            var index = checks.FindIndex(c => c.Identity == identity);
            if (index >= 0 && checks[index].Options is not null) continue;
            var check = index >= 0 ? checks[index] : new GlitchTipMonitorAnnotation(identity, probe.EndpointReference, probe.Path, 200);
            check = check with
            {
                Options = new()
                {
                    IntervalSeconds = probe.PeriodSeconds > 0 ? probe.PeriodSeconds : resource.MonitorDefaults.IntervalSeconds,
                    TimeoutSeconds = probe.TimeoutSeconds > 0 ? probe.TimeoutSeconds : resource.MonitorDefaults.TimeoutSeconds
                }
            };
            if (index >= 0) checks[index] = check;
            else checks.Add(check);
        }
        return checks;
    }
    internal static async Task SynchronizeAsync(IResourceBuilder<GlitchTipResource> builder, IEnumerable<IResource> resources,
        CancellationToken cancellationToken, PipelineStepContext? deployment = null)
    {
        var resource = builder.Resource;
        var management = resource.Management ?? throw new DistributedApplicationException("GlitchTip project must be provisioned before monitors.");
        var project = await resource.Provisioned.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        var desired = new List<GlitchTipMonitorDefinition>();
        foreach (var consumer in resources.Where(r => r.Annotations.OfType<GlitchTipConsumerAnnotation>().Any(a => a.Project == resource)))
        {
            if (deployment is not null && consumer.IsExcludedFromPublish()) continue;
            var checks = GetChecks(consumer, resource);
            foreach (var check in checks.Where(c => c.Enabled))
            {
                string address;
                if (check.Url is not null)
                {
                    address = (deployment is null
                        ? await check.Url.GetValueAsync(new ValueProviderContext { Caller = resource.LocalServer, ExecutionContext = builder.ApplicationBuilder.ExecutionContext }, cancellationToken).ConfigureAwait(false)
                        : await GlitchTipDeploymentPipeline.ResolveExpressionAsync(deployment, check.Url).ConfigureAwait(false))
                        ?? throw new DistributedApplicationException("GlitchTip monitor URL resolved to an empty value.");
                }
                else
                {
                    var endpoint = deployment is null
                        ? await ((IValueProvider)check.Endpoint).GetValueAsync(new ValueProviderContext { Caller = resource.LocalServer, ExecutionContext = builder.ApplicationBuilder.ExecutionContext }, cancellationToken).ConfigureAwait(false)
                        : await GlitchTipDeploymentPipeline.ResolveEndpointAsync(deployment, check.Endpoint).ConfigureAwait(false);
                    address = new Uri(new Uri(endpoint!), check.Path).AbsoluteUri;
                }
                var settings = check.Options ?? resource.MonitorDefaults;
                desired.Add(new($"{consumer.Name}:{check.Identity}", address, settings.IntervalSeconds, settings.TimeoutSeconds, check.StatusCode));
            }
        }
        using var client = new GlitchTipManagementClient(management.Instance, management.Token);
        await client.ReconcileMonitorsAsync(management.Organization, project.Id, resource.EnvironmentName, desired, cancellationToken).ConfigureAwait(false);
    }
}
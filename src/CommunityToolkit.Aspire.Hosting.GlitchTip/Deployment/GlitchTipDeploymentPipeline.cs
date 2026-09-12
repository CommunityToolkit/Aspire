// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker;
using Aspire.Hosting.Pipelines;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;

internal static class GlitchTipDeploymentPipeline
{
    internal static IResourceBuilder<T> Configure<T>(
        IResourceBuilder<T> builder,
        Func<PipelineStepContext, Task> provision,
        Func<PipelineStepContext, Task> synchronize) where T : IResource
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder;
        }

        builder.WithPipelineStepFactory(_ => CreateSteps(builder.Resource, provision, synchronize));
        builder.WithPipelineConfiguration(context => ConfigureDependencies(builder.Resource, context));
        return builder;
    }

    internal static PipelineStep[] CreateSteps(
        IResource resource,
        Func<PipelineStepContext, Task> provision,
        Func<PipelineStepContext, Task> synchronize) =>
    [
        new()
        {
            Name = ProvisionStepName(resource),
            Description = $"Resolve the GlitchTip project and reporting credentials for '{resource.Name}'",
            Resource = resource,
            DependsOnSteps = [WellKnownPipelineSteps.DeployPrereq, WellKnownPipelineSteps.ProcessParameters],
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            Action = async context =>
            {
                if (context.Model.Resources.OfType<IComputeEnvironmentResource>().Any(environment => environment is not DockerComposeEnvironmentResource))
                {
                    throw new DistributedApplicationException("GlitchTip deployment ordering currently supports Docker Compose compute environments. Use a dedicated control-plane AppHost when another tool owns application rollout.");
                }

                await provision(context).ConfigureAwait(false);
            }
        },
        new()
        {
            Name = SynchronizeStepName(resource),
            Description = $"Reconcile GlitchTip monitors and upload registered artifacts for '{resource.Name}'",
            Resource = resource,
            DependsOnSteps = [ProvisionStepName(resource), WellKnownPipelineSteps.Build],
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            Action = synchronize
        }
    ];

    internal static void ConfigureDependencies(IResource resource, PipelineConfigurationContext context)
    {
        var synchronize = context.Steps.Single(step => step.Name == SynchronizeStepName(resource));
        foreach (var environment in context.Model.Resources.OfType<DockerComposeEnvironmentResource>())
        {
            // Aspire.Hosting.Docker 13.5 resolves custom IValueProvider sources in prepare.
            // Provision first so its fresh in-memory DSN is used by this deployment's env file.
            var prepare = context.Steps.SingleOrDefault(step => step.Name == $"prepare-{environment.Name}");
            prepare?.DependsOn(ProvisionStepName(resource));
            if (prepare is not null)
            {
                synchronize.DependsOn(prepare.Name);
            }

            context.Steps.SingleOrDefault(step => step.Name == $"docker-compose-up-{environment.Name}")
                ?.DependsOn(synchronize.Name);
        }
    }

    internal static async Task<string?> ResolveExpressionAsync(PipelineStepContext context, ReferenceExpression expression)
    {
        // Evaluate only the selected branch. An unused branch may deliberately refer to
        // a resource which has no deployment target or an unavailable secret.
        if (expression.IsConditional)
        {
            var condition = await ResolveValueAsync(context, expression.Condition!).ConfigureAwait(false);
            var branch = string.Equals(condition, expression.MatchValue, StringComparison.OrdinalIgnoreCase)
                ? expression.WhenTrue! : expression.WhenFalse!;
            return await ResolveExpressionAsync(context, branch).ConfigureAwait(false);
        }
        if (expression.Format.Length == 0)
        {
            return null;
        }
        var values = new object?[expression.ValueProviders.Count];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = await ResolveValueAsync(context, expression.ValueProviders[index]).ConfigureAwait(false);
            if (expression.StringFormats[index] is { } format && values[index] is string value)
            {
                values[index] = string.Equals(format, "uri", StringComparison.OrdinalIgnoreCase)
                    ? Uri.EscapeDataString(value)
                    : throw new NotSupportedException("GlitchTip monitor expressions support the Aspire 'uri' string format.");
            }
        }
        return string.Format(CultureInfo.InvariantCulture, expression.Format, values);
    }

    private static async Task<string?> ResolveValueAsync(PipelineStepContext context, IValueProvider value)
    {
        switch (value)
        {
            case EndpointReference endpoint:
                return await ResolveEndpointAsync(context, endpoint).ConfigureAwait(false);
            case EndpointReferenceExpression property:
                return await ResolveEndpointPropertyAsync(context, property).ConfigureAwait(false);
            case ReferenceExpression expression:
                return await ResolveExpressionAsync(context, expression).ConfigureAwait(false);
            case ConnectionStringReference connection:
                var result = await ResolveExpressionAsync(context, connection.Resource.ConnectionStringExpression).ConfigureAwait(false);
                if (string.IsNullOrEmpty(result) && !connection.Optional)
                {
                    throw new DistributedApplicationException($"The connection string for the resource '{connection.Resource.Name}' is not available.");
                }
                return result;
            case IResourceWithConnectionString resource:
                return await ResolveExpressionAsync(context, resource.ConnectionStringExpression).ConfigureAwait(false);
            default:
                // Preserve normal parameter and secret provider semantics. Values are
                // consumed in memory and never exported to a manifest or pipeline log.
                return await value.GetValueAsync(new ValueProviderContext { ExecutionContext = context.ExecutionContext }, context.CancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<string> ResolveEndpointAsync(PipelineStepContext context, EndpointReference endpoint) =>
        await ResolveEndpointPropertyAsync(context, endpoint.Property(EndpointProperty.Url)).ConfigureAwait(false);

    private static async Task<string> ResolveEndpointPropertyAsync(PipelineStepContext context, EndpointReferenceExpression property)
    {
        var endpoint = property.Endpoint;
        if (property.Property == EndpointProperty.Scheme)
        {
            return endpoint.Scheme;
        }
        if (property.Property == EndpointProperty.TlsEnabled)
        {
            return endpoint.TlsEnabled ? bool.TrueString : bool.FalseString;
        }
        var environment = endpoint.Resource.GetDeploymentTargetAnnotation()?.ComputeEnvironment;
        if (environment is not DockerComposeEnvironmentResource)
        {
            throw new DistributedApplicationException($"GlitchTip cannot translate the monitor endpoint for '{endpoint.Resource.Name}' without a Docker Compose deployment target.");
        }

#pragma warning disable ASPIRECOMPUTE002
        // Match Compose's normal endpoint-reference translation, which uses the service
        // hostname and internal target port, not the externally published host port.
        var host = environment.GetHostAddressExpression(endpoint);
        var port = environment.GetEndpointPropertyExpression(endpoint.Property(EndpointProperty.TargetPort));
#pragma warning restore ASPIRECOMPUTE002
        var expression = property.Property switch
        {
            EndpointProperty.Url => ReferenceExpression.Create($"{endpoint.Scheme}://{host}:{port}"),
            EndpointProperty.Host or EndpointProperty.IPV4Host => host,
            EndpointProperty.Port or EndpointProperty.TargetPort => port,
            EndpointProperty.HostAndPort => ReferenceExpression.Create($"{host}:{port}"),
            _ => throw new DistributedApplicationException($"GlitchTip does not support monitor endpoint property '{property.Property}'.")
        };
        return await expression.GetValueAsync(context.CancellationToken).ConfigureAwait(false)
            ?? throw new DistributedApplicationException($"GlitchTip could not translate the monitor endpoint for '{endpoint.Resource.Name}'.");
    }

    private static string ProvisionStepName(IResource resource) => $"glitchtip-provision-{resource.Name}";

    private static string SynchronizeStepName(IResource resource) => $"glitchtip-synchronize-{resource.Name}";
}
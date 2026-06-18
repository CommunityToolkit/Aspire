// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Pipeline APIs are experimental
#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Kubernetes;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for configuring a <see cref="KubernetesEnvironmentResource"/>
/// to deploy to a local Kind cluster.
/// </summary>
public static class KubernetesEnvironmentKindExtensions
{
    /// <summary>
    /// Configures the Kubernetes environment to create and deploy to a local
    /// Kind cluster. Enables <c>aspire deploy</c> to provision the cluster,
    /// load container images, and install the generated Helm chart.
    /// </summary>
    /// <param name="builder">The Kubernetes environment resource builder.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{KindEnvironmentResource}"/>
    /// for further Kind-specific configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<KindEnvironmentResource> WithKind(
        this IResourceBuilder<KubernetesEnvironmentResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ApplicationBuilder.Services.AddKindInfrastructure();

        var kindResource = new KindEnvironmentResource($"{builder.Resource.Name}-kind", builder.Resource);

        IResourceBuilder<KindEnvironmentResource> kindBuilder;

        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            // In run mode, don't surface the Kind environment in the dashboard.
            // The KubernetesEnvironmentResource is also invisible in run mode.
            kindBuilder = builder.ApplicationBuilder.CreateResourceBuilder(kindResource);
        }
        else
        {
            kindBuilder = builder.ApplicationBuilder.AddResource(kindResource);

            kindBuilder.WithAnnotation(new PipelineStepAnnotation(ctx =>
                KindDeployPipelineSteps.CreateSteps(
                    ctx.Resource as KindEnvironmentResource
                        ?? throw new InvalidOperationException(
                            $"Expected resource of type {nameof(KindEnvironmentResource)}"))));
        }

        return kindBuilder;
    }
}

#pragma warning restore ASPIREATS001

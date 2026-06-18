// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Pipeline APIs are experimental
#pragma warning disable ASPIREPIPELINES003 // ContainerBuildOptionsCallbackAnnotation is experimental
#pragma warning disable ASPIREPIPELINES004 // IPipelineOutputService is experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using CommunityToolkit.Aspire.Hosting.Kind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Creates Kind-specific deploy pipeline steps for cluster creation,
/// image loading, and Helm chart installation.
/// </summary>
internal static class KindDeployPipelineSteps
{
    /// <summary>
    /// Creates the pipeline steps for deploying to a Kind cluster.
    /// </summary>
    internal static IEnumerable<PipelineStep> CreateSteps(KindEnvironmentResource kindResource)
    {
        var clusterName = kindResource.Name;

        var createCluster = new PipelineStep
        {
            Name = $"kind-create-cluster-{clusterName}",
            Description = $"Creates Kind cluster '{clusterName}'",
            Resource = kindResource,
            Action = async ctx =>
            {
                var processRunner = ctx.Services.GetRequiredService<IProcessRunner>();
                var containerRuntimeResolver = ctx.Services.GetRequiredService<IKindContainerRuntimeResolver>();
                var manager = new KindClusterManager(kindResource, ctx.Logger, processRunner, containerRuntimeResolver);
                await manager.CreateClusterAsync(ctx.CancellationToken);
            }
        };
        createCluster.DependsOn(WellKnownPipelineSteps.DeployPrereq);

        var loadImages = new PipelineStep
        {
            Name = $"kind-load-images-{clusterName}",
            Description = $"Loads container images into Kind cluster '{clusterName}'",
            Resource = kindResource,
            Action = async ctx =>
            {
                await PreloadImagesAsync(kindResource, ctx);
            }
        };
        loadImages.DependsOn(createCluster);
        loadImages.DependsOn(WellKnownPipelineSteps.Build);

        var helmInstall = new PipelineStep
        {
            Name = $"kind-helm-install-{clusterName}",
            Description = $"Deploys Helm chart to Kind cluster '{clusterName}'",
            Resource = kindResource,
            Action = async ctx =>
            {
                await HelmInstallAsync(kindResource, ctx);
            }
        };
        helmInstall.DependsOn(createCluster);
        helmInstall.DependsOn(loadImages);
        helmInstall.DependsOn(WellKnownPipelineSteps.Publish);
        helmInstall.RequiredBy(WellKnownPipelineSteps.Deploy);

        return [createCluster, loadImages, helmInstall];
    }

    private static async Task PreloadImagesAsync(KindEnvironmentResource kindResource, PipelineStepContext ctx)
    {
        var processRunner = ctx.Services.GetRequiredService<IProcessRunner>();
        var containerRuntimeResolver = ctx.Services.GetRequiredService<IKindContainerRuntimeResolver>();
        var containerRuntime = await containerRuntimeResolver.ResolveAsync(ctx.CancellationToken).ConfigureAwait(false);

        foreach (var resource in ctx.Model.Resources)
        {
            // Only load locally-built images (from ContainerBuildOptionsCallbackAnnotation).
            // Public registry images (from ContainerImageAnnotation) are pulled by Kind nodes
            // directly and don't need preloading.
            var imageName = await GetLocallyBuiltImageNameAsync(resource, ctx);
            if (imageName is null)
            {
                continue;
            }

            ctx.Logger.LogInformation("Loading image {Image} into Kind cluster '{Cluster}'", imageName, kindResource.Name);

            var result = await processRunner.RunAsync(
                ctx.Logger,
                "kind",
                ["load", "docker-image", imageName, "--name", kindResource.Name],
                environmentVariables: containerRuntime.KindEnvironmentVariables,
                cancellationToken: ctx.CancellationToken);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Failed to load image '{imageName}' into Kind cluster '{kindResource.Name}': {result.Error}");
            }
        }
    }

    /// <summary>
    /// Returns the locally-built image name for a resource, or <see langword="null"/> if the
    /// resource uses a public registry image that Kind can pull on its own.
    /// </summary>
    private static async Task<string?> GetLocallyBuiltImageNameAsync(IResource resource, PipelineStepContext ctx)
    {
        // ProjectResource and similar resources use ContainerBuildOptionsCallbackAnnotation
        // to configure the local image name after building via dotnet publish /t:PublishContainer.
        if (resource.TryGetAnnotationsOfType<ContainerBuildOptionsCallbackAnnotation>(out var annotations))
        {
            var buildContext = new ContainerBuildOptionsCallbackContext(
                resource, ctx.Services, ctx.Logger, ctx.CancellationToken, ctx.ExecutionContext);

            foreach (var annotation in annotations)
            {
                await annotation.Callback(buildContext);
            }

            if (buildContext.LocalImageName is not null)
            {
                var tag = buildContext.LocalImageTag ?? "latest";
                return $"{buildContext.LocalImageName}:{tag}";
            }
        }

        return null;
    }

    private static async Task HelmInstallAsync(KindEnvironmentResource kindResource, PipelineStepContext ctx)
    {
        var processRunner = ctx.Services.GetRequiredService<IProcessRunner>();

        ctx.Logger.LogInformation("Deploying Helm chart to Kind cluster '{Cluster}'", kindResource.Name);

        var releaseName = kindResource.Name;
        var kubeconfigPath = kindResource.KubeconfigPath;

        var outputService = ctx.Services.GetRequiredService<IPipelineOutputService>();

        // Use the same logic as PublishingContextUtils.GetEnvironmentOutputPath:
        // root output path when there's one compute environment, resource-specific when multiple.
        var computeEnvironments = ctx.Model.Resources.OfType<IComputeEnvironmentResource>().Count();
        var chartPath = computeEnvironments > 1
            ? outputService.GetOutputDirectory(kindResource.Parent)
            : outputService.GetOutputDirectory();

        ctx.Logger.LogInformation("Using Helm chart from '{ChartPath}'", chartPath);

        var result = await processRunner.RunAsync(
            ctx.Logger,
            "helm",
            ["upgrade", "--install", releaseName, chartPath, "--kubeconfig", kubeconfigPath, "--wait", "--timeout", "5m"],
            cancellationToken: ctx.CancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Helm install failed for Kind cluster '{kindResource.Name}': {result.Error}");
        }

        ctx.Logger.LogInformation("Helm chart deployed to Kind cluster '{Cluster}'", kindResource.Name);
    }
}

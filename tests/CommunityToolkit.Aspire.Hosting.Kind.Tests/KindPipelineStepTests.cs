// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Pipeline APIs are experimental

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Kubernetes;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindPipelineStepTests
{
    [Fact]
    public void WithKindAddsPipelineStepsInPublishMode()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddKubernetesEnvironment("k8s").WithKind();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var kindResource = Assert.Single(model.Resources.OfType<KindEnvironmentResource>());
        Assert.True(kindResource.TryGetAnnotationsOfType<PipelineStepAnnotation>(out var annotations));
        Assert.Single(annotations);
    }

    [Fact]
    public void CreateStepsReturnsExpectedStepNames()
    {
        var k8sEnv = new KubernetesEnvironmentResource("k8s");
        var kindResource = new KindEnvironmentResource("k8s-kind", k8sEnv);

        var steps = KindDeployPipelineSteps.CreateSteps(kindResource).ToList();

        Assert.Contains(steps, s => s.Name == "kind-create-cluster-k8s-kind");
        Assert.Contains(steps, s => s.Name == "kind-load-images-k8s-kind");
        Assert.Contains(steps, s => s.Name == "kind-helm-install-k8s-kind");
        Assert.Equal(3, steps.Count);
    }

    [Fact]
    public void CreateClusterStepDependsOnDeployPrereq()
    {
        var k8sEnv = new KubernetesEnvironmentResource("k8s");
        var kindResource = new KindEnvironmentResource("k8s-kind", k8sEnv);

        var steps = KindDeployPipelineSteps.CreateSteps(kindResource).ToList();
        var createStep = steps.Single(s => s.Name == "kind-create-cluster-k8s-kind");

        Assert.Contains(WellKnownPipelineSteps.DeployPrereq, createStep.DependsOnSteps);
    }

    [Fact]
    public void HelmInstallStepDependsOnCreateClusterAndPublishAndLoadImages()
    {
        var k8sEnv = new KubernetesEnvironmentResource("k8s");
        var kindResource = new KindEnvironmentResource("k8s-kind", k8sEnv);

        var steps = KindDeployPipelineSteps.CreateSteps(kindResource).ToList();
        var helmStep = steps.Single(s => s.Name == "kind-helm-install-k8s-kind");

        Assert.Contains("kind-create-cluster-k8s-kind", helmStep.DependsOnSteps);
        Assert.Contains("kind-load-images-k8s-kind", helmStep.DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.Publish, helmStep.DependsOnSteps);
    }

    [Fact]
    public void HelmInstallStepIsRequiredByDeploy()
    {
        var k8sEnv = new KubernetesEnvironmentResource("k8s");
        var kindResource = new KindEnvironmentResource("k8s-kind", k8sEnv);

        var steps = KindDeployPipelineSteps.CreateSteps(kindResource).ToList();
        var helmStep = steps.Single(s => s.Name == "kind-helm-install-k8s-kind");

        Assert.Contains(WellKnownPipelineSteps.Deploy, helmStep.RequiredBySteps);
    }

    [Fact]
    public void LoadImagesStepDependsOnCreateClusterAndBuild()
    {
        var k8sEnv = new KubernetesEnvironmentResource("k8s");
        var kindResource = new KindEnvironmentResource("k8s-kind", k8sEnv);

        var steps = KindDeployPipelineSteps.CreateSteps(kindResource).ToList();
        var loadStep = steps.Single(s => s.Name == "kind-load-images-k8s-kind");

        Assert.Contains("kind-create-cluster-k8s-kind", loadStep.DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.Build, loadStep.DependsOnSteps);
    }

    [Fact]
    public void AllStepsHaveResourceSet()
    {
        var k8sEnv = new KubernetesEnvironmentResource("k8s");
        var kindResource = new KindEnvironmentResource("k8s-kind", k8sEnv);

        var steps = KindDeployPipelineSteps.CreateSteps(kindResource).ToList();

        Assert.All(steps, s => Assert.Same(kindResource, s.Resource));
    }
}

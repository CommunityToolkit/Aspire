// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Management;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class LocalArtifactTests
{
    [Fact]
    public void LocalUploadOptionIsExplicitAndKeepsConsumerCompletionDependency()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder);
        Assert.False(project.Resource.LocalArtifactUploadsEnabled);
        var consumer = builder.AddContainer("api", "unused-image").WithReference(project);
        var count = builder.Resources.Count;

        Assert.Same(project, project.WithLocalArtifactUploads());
        Assert.True(project.Resource.LocalArtifactUploadsEnabled);
        var wait = Assert.Single(consumer.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(project.Resource, wait.Resource);
        Assert.Equal(WaitType.WaitForCompletion, wait.WaitType);
        Assert.Equal(count, builder.Resources.Count);

        project.WithLocalArtifactUploads(enabled: false);
        Assert.False(project.Resource.LocalArtifactUploadsEnabled);
    }

    [Fact]
    public async Task DefaultLocalStartupDoesNotInspectArtifactRegistrations()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder);
        builder.AddContainer("api", "unused-image").WithGlitchTipDebugSymbols(MissingPath());

        // Even the missing reference is irrelevant while local uploads are off.
        await GlitchTipArtifacts.UploadLocalAsync(project, builder.Resources, TestContext.Current.CancellationToken);

        Assert.Null(project.Resource.Management);
        Assert.False(project.Resource.Provisioned.Task.IsCompleted);
    }

    [Fact]
    public async Task EnabledLocalUploadsIncludeResourcesExcludedFromDeployment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder).WithLocalArtifactUploads();
        SetProvisioned(project);
        builder.AddContainer("api", "unused-image").ExcludeFromManifest()
            .WithGlitchTipDebugSymbols(MissingPath()).WithReference(project);

        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            GlitchTipArtifacts.UploadLocalAsync(project, builder.Resources, TestContext.Current.CancellationToken));

        Assert.Contains("required GlitchTip artifact registration is missing", error.Message);
    }

    [Fact]
    public async Task OptionalLocalArtifactAbsenceSucceedsWithoutContactingManagement()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder).WithLocalArtifactUploads();
        SetProvisioned(project);
        builder.AddContainer("api", "unused-image").WithGlitchTipSourceMaps(MissingPath(), optional: true).WithReference(project);

        await GlitchTipArtifacts.UploadLocalAsync(project, builder.Resources, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EnabledLocalArtifactsRequireTheOwningProjectReference()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder).WithLocalArtifactUploads();
        builder.AddContainer("api", "unused-image").WithGlitchTipDebugSymbols(MissingPath());

        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            GlitchTipArtifacts.UploadLocalAsync(project, builder.Resources, TestContext.Current.CancellationToken));

        Assert.Contains("does not reference the GlitchTip project", error.Message);
    }

    [Fact]
    public async Task LocalArtifactsUseTheServiceReleaseOverride()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var project = AddProject(builder).WithLocalArtifactUploads();
        var emptyServiceRelease = builder.AddParameter("service-release", "");
        builder.AddContainer("api", "unused-image").WithGlitchTipDebugSymbols(MissingPath())
            .WithGlitchTipRelease(emptyServiceRelease).WithReference(project);

        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            GlitchTipArtifacts.UploadLocalAsync(project, builder.Resources, TestContext.Current.CancellationToken));

        Assert.Contains("'service-release' is empty", error.Message);
    }

    [Fact]
    public async Task LocalOptionDoesNotEnablePublishTimeWork()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder).WithLocalArtifactUploads();
        builder.AddContainer("api", "unused-image").WithGlitchTipDebugSymbols(MissingPath()).WithReference(project);

        Assert.False(project.Resource.LocalArtifactUploadsEnabled);
        await GlitchTipArtifacts.UploadLocalAsync(project, builder.Resources, TestContext.Current.CancellationToken);
        Assert.Null(project.Resource.Management);
    }

    [Fact]
    public async Task DeploymentStillSkipsExcludedResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder).WithLocalArtifactUploads();
        builder.AddContainer("api", "unused-image").ExcludeFromManifest().WithGlitchTipDebugSymbols(MissingPath());

        await GlitchTipArtifacts.UploadAsync(project, builder.Resources, TestContext.Current.CancellationToken);
        Assert.Null(project.Resource.Management);
    }

    [Fact]
    public async Task DeploymentStillUploadsWithoutLocalOptIn()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var project = AddProject(builder);
        SetProvisioned(project);
        builder.AddContainer("api", "unused-image").WithGlitchTipDebugSymbols(MissingPath()).WithReference(project);

        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            GlitchTipArtifacts.UploadAsync(project, builder.Resources, TestContext.Current.CancellationToken));

        Assert.Contains("required GlitchTip artifact registration is missing", error.Message);
    }

    private static string MissingPath() => Path.Combine(Path.GetTempPath(), "glitchtip-missing-" + Guid.NewGuid().ToString("N"));

    private static IResourceBuilder<GlitchTipResource> AddProject(IDistributedApplicationBuilder builder) =>
        builder.AddGlitchTip("glitchtip", builder.AddParameter("project", "stack"), builder.AddParameter("release", "local"));

    private static void SetProvisioned(IResourceBuilder<GlitchTipResource> project)
    {
        // Missing and optional files are handled before any network call. This
        // deliberately unreachable origin ensures these tests cannot mutate a server.
        project.Resource.Management = (new Uri("https://not-contacted.invalid/"), "test-token", "aspire");
        project.Resource.Provisioned.SetResult(new GlitchTipProject("1", "stack", "https://test@not-contacted.invalid/1"));
    }
}
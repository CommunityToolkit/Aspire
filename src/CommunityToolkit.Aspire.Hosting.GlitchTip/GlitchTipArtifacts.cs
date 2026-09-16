// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip;

internal sealed record GlitchTipArtifactAnnotation(string Path, bool Optional, GlitchTipArtifactKind Kind) : IResourceAnnotation;

internal static class GlitchTipArtifacts
{
    internal static Task UploadLocalAsync(IResourceBuilder<GlitchTipResource> builder, IEnumerable<IResource> resources, CancellationToken ct) =>
        builder.ApplicationBuilder.ExecutionContext.IsRunMode && builder.Resource.LocalArtifactUploadsEnabled
            ? UploadAsync(builder, resources, ct)
            : Task.CompletedTask;

    internal static async Task UploadAsync(IResourceBuilder<GlitchTipResource> builder, IEnumerable<IResource> resources, CancellationToken ct)
    {
        var project = builder.Resource;
        var registrations = new List<GlitchTipArtifact>();
        foreach (var consumer in resources)
        {
            if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode && consumer.IsExcludedFromPublish()) continue;
            var artifacts = consumer.Annotations.OfType<GlitchTipArtifactAnnotation>().ToArray();
            if (artifacts.Length == 0) continue;
            if (!consumer.Annotations.OfType<GlitchTipConsumerAnnotation>().Any(a => a.Project == project))
                throw new DistributedApplicationException($"Resource '{consumer.Name}' registers GlitchTip artifacts but does not reference the GlitchTip project.");
            var releaseParameter = consumer.Annotations.OfType<GlitchTipReleaseAnnotation>().LastOrDefault()?.Release ?? project.Release;
            var release = await GlitchTipLifecycle.RequiredAsync(releaseParameter, "artifact release", ct).ConfigureAwait(false);
            registrations.AddRange(artifacts.Select(a => new GlitchTipArtifact(a.Path, release, a.Optional, a.Kind)));
        }
        if (registrations.Count == 0) return;
        var (instance, token, organization) = project.Management ?? throw new DistributedApplicationException("GlitchTip project must be provisioned before artifact uploads.");
        var remote = await project.Provisioned.Task.WaitAsync(ct).ConfigureAwait(false);
        await GlitchTipArtifactUploader.UploadAsync(instance, token, organization, remote.Slug, registrations, builder.ApplicationBuilder.AppHostDirectory, ct).ConfigureAwait(false);
    }
}
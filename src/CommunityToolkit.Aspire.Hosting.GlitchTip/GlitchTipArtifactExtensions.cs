// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.GlitchTip;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Deployment;

#pragma warning disable IDE0130 // Hosting extensions and resources use the Aspire public API namespaces.
namespace Aspire.Hosting;
#pragma warning restore IDE0130

/// <summary>Explicit artifact registrations for GlitchTip deployment and opt-in local startup.</summary>
public static class GlitchTipArtifactExtensions
{
    /// <summary>Uploads registered build outputs during local setup before dependent resources start. Disabled by default; does not build or watch artifacts.</summary>
    /// <param name="builder">The GlitchTip project resource.</param>
    /// <param name="enabled">Whether local setup uploads registered artifacts.</param>
    /// <returns>The original resource builder. This setting has no effect on deployment.</returns>
    [AspireExport]
    public static IResourceBuilder<GlitchTipResource> WithLocalArtifactUploads(this IResourceBuilder<GlitchTipResource> builder, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            builder.Resource.LocalArtifactUploadsEnabled = enabled;
        }
        return builder;
    }
    /// <summary>Registers JavaScript and source maps prepared with matching debug IDs for deployment and enabled local uploads.</summary>
    /// <typeparam name="T">The reporting service.</typeparam>
    /// <param name="builder">The consumer resource.</param>
    /// <param name="path">A file or directory, relative to the AppHost directory.</param>
    /// <param name="optional">Whether an absent artifact path is expected.</param>
    /// <returns>The original resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithGlitchTipSourceMaps<T>(this IResourceBuilder<T> builder, string path, bool optional = false) where T : IResourceWithEnvironment =>
        Add(builder, path, optional, GlitchTipArtifactKind.SourceMaps);

    /// <summary>Registers dSYM, PDB, or ELF debug artifacts for deployment and enabled local uploads, verifying server processing.</summary>
    /// <typeparam name="T">The reporting service.</typeparam>
    /// <param name="builder">The consumer resource.</param>
    /// <param name="path">A file or directory, relative to the AppHost directory.</param>
    /// <param name="optional">Whether an absent artifact path is expected.</param>
    /// <returns>The original resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<T> WithGlitchTipDebugSymbols<T>(this IResourceBuilder<T> builder, string path, bool optional = false) where T : IResourceWithEnvironment =>
        Add(builder, path, optional, GlitchTipArtifactKind.DebugSymbols);

    private static IResourceBuilder<T> Add<T>(IResourceBuilder<T> builder, string path, bool optional, GlitchTipArtifactKind kind) where T : IResourceWithEnvironment
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var absolutePath = Path.GetFullPath(path, builder.ApplicationBuilder.AppHostDirectory);
        return builder.WithAnnotation(new GlitchTipArtifactAnnotation(absolutePath, optional, kind));
    }
}
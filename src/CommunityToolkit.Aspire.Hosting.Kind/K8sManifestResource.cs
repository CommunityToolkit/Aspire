// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

#pragma warning disable ASPIREATS001 // AspireExport APIs are experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A Kubernetes manifest applied to a Kind cluster via <c>kubectl apply</c>.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="manifestPath">
/// Absolute path to a Kubernetes manifest file or directory of manifest files,
/// or <c>&lt;inline&gt;</c> for manifests provided via standard input.
/// </param>
/// <param name="parent">The parent Kind cluster resource.</param>
[AspireExport(ExposeProperties = true)]
public class K8sManifestResource(string name, string manifestPath, KindClusterResource parent)
    : KindDeployedResource(name, parent)
{
    internal const string InlineManifestPath = "<inline>";

    /// <summary>
    /// Gets the manifest path passed to <c>kubectl apply</c>.
    /// Accepts a file path, a directory path, or <c>&lt;inline&gt;</c> for standard input.
    /// </summary>
    public string ManifestPath { get; } = manifestPath ?? throw new ArgumentNullException(nameof(manifestPath));

    /// <summary>
    /// Gets or sets whether this resource represents a Kustomize overlay directory.
    /// </summary>
    public bool IsKustomize { get; set; }

    /// <summary>
    /// Gets or sets inline manifest content applied with <c>kubectl apply -f -</c>.
    /// </summary>
    public string? InlineContent { get; set; }

    /// <summary>
    /// Gets or sets whether to recursively apply manifests in subdirectories
    /// (maps to <c>kubectl apply --recursive</c>).
    /// Only meaningful when <see cref="ManifestPath"/> is a directory.
    /// </summary>
    public bool Recursive { get; set; }

    /// <summary>
    /// Gets or sets whether to apply the manifest server-side
    /// (maps to <c>kubectl apply --server-side</c>).
    /// Server-side apply is required for large CRDs that exceed the client-side annotation size limit.
    /// </summary>
    public bool ServerSide { get; set; }

    /// <summary>
    /// Gets or sets whether to force conflicts on server-side apply
    /// (maps to <c>kubectl apply --server-side --force-conflicts</c>).
    /// Only meaningful when <see cref="ServerSide"/> is <see langword="true"/>.
    /// </summary>
    public bool ForceConflicts { get; set; }

    /// <summary>
    /// Gets the field manager name used with server-side apply
    /// (maps to <c>kubectl apply --field-manager</c>).
    /// When <see langword="null"/>, kubectl uses its default (<c>kubectl</c>).
    /// </summary>
    public string? FieldManager { get; set; }

    /// <summary>
    /// Gets or sets the maximum time to wait for <c>kubectl apply</c> to complete.
    /// </summary>
    public TimeSpan ApplyTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum time to wait for the Kubernetes API to become reachable
    /// before running <c>kubectl apply</c>.
    /// </summary>
    public TimeSpan ClusterReadyTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the maximum time to wait for applied CRDs to reach the <c>Established</c> condition.
    /// </summary>
    public TimeSpan CrdWaitTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how CRD wait failures are handled.
    /// </summary>
    public CrdWaitBehavior CrdWaitBehavior { get; set; } = CrdWaitBehavior.Fail;
}

#pragma warning restore ASPIREATS001

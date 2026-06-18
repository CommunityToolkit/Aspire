// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A Helm chart deployed to a Kind cluster.
/// </summary>
/// <param name="name">The name of the resource (also used as the Helm release name).</param>
/// <param name="chartRef">
/// Chart reference passed to <c>helm install</c>. Any form Helm supports:
/// OCI (<c>oci://registry/chart</c>), repo (<c>repo/chart</c>),
/// or local path (<c>./charts/my-app</c>).
/// </param>
/// <param name="parent">The parent Kind cluster resource.</param>
public class KindHelmChartResource(string name, string chartRef, KindClusterResource parent)
    : KindDeployedResource(name, parent)
{
    /// <summary>
    /// Gets the chart reference passed to <c>helm install</c>.
    /// </summary>
    public string ChartRef { get; } = chartRef ?? throw new ArgumentNullException(nameof(chartRef));

    /// <summary>
    /// Gets or sets the Helm chart version (maps to <c>--version</c>).
    /// When <see langword="null"/>, Helm uses the latest version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets the Helm release name. Defaults to the resource name.
    /// </summary>
    public string ReleaseName => Name;

    /// <summary>
    /// Gets the inline Helm values (each maps to <c>--set key=value</c>).
    /// </summary>
    public Dictionary<string, string> Values { get; } = [];

    /// <summary>
    /// Gets the paths to values files (each maps to <c>-f path</c>).
    /// </summary>
    public List<string> ValuesFiles { get; } = [];
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Kind cluster configuration document (Kind v1alpha4).
/// </summary>
/// <remarks>
/// See <see href="https://kind.sigs.k8s.io/docs/user/configuration/">Kind configuration</see> for details.
/// </remarks>
public sealed class KindConfigModel
{
    /// <summary>
    /// Gets or sets the API version for the Kind configuration.
    /// </summary>
    public string ApiVersion { get; set; } = "kind.x-k8s.io/v1alpha4";

    /// <summary>
    /// Gets or sets the resource kind.
    /// </summary>
    public string Kind { get; set; } = "Cluster";

    /// <summary>
    /// Gets the list of nodes in the cluster.
    /// </summary>
    public IList<KindNodeModel> Nodes { get; } = [];

    /// <summary>
    /// Gets or sets cluster-wide network settings.
    /// When <see langword="null"/>, Kind uses its defaults.
    /// </summary>
    public KindNetworkingModel? Networking { get; set; }

    /// <summary>
    /// Gets or sets Kubernetes feature gates to enable or disable.
    /// Keys are feature gate names; values indicate whether the gate is enabled.
    /// When <see langword="null"/>, no feature gates are configured.
    /// </summary>
    public IDictionary<string, bool>? FeatureGates { get; set; }

    /// <summary>
    /// Gets or sets runtime config values passed to kube-apiserver as <c>--runtime-config</c>.
    /// Use this to enable alpha APIs.
    /// When <see langword="null"/>, no runtime config is set.
    /// </summary>
    public IDictionary<string, string>? RuntimeConfig { get; set; }

    /// <summary>
    /// Gets or sets kubeadm config patches applied as merge patches at the cluster level.
    /// Each entry is a YAML string. Cluster-level patches are applied before node-level patches.
    /// When <see langword="null"/>, no cluster-level kubeadm patches are applied.
    /// </summary>
    public IList<string>? KubeadmConfigPatches { get; set; }

    /// <summary>
    /// Gets or sets containerd config patches applied to every node.
    /// Each entry is a TOML merge-patch string.
    /// When <see langword="null"/>, no containerd patches are applied.
    /// </summary>
    public IList<string>? ContainerdConfigPatches { get; set; }
}

/// <summary>
/// Represents a node in a Kind cluster configuration.
/// </summary>
public sealed class KindNodeModel
{
    /// <summary>
    /// Gets or sets the role of the node (e.g., "control-plane" or "worker").
    /// </summary>
    public string Role { get; set; } = "control-plane";

    /// <summary>
    /// Gets or sets the container image for the node.
    /// When <see langword="null"/>, Kind uses its default image.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Gets or sets labels to apply to the node.
    /// When <see langword="null"/>, no additional labels are applied.
    /// </summary>
    public IDictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// Gets or sets additional mount points for the node container.
    /// When <see langword="null"/>, no extra mounts are configured.
    /// </summary>
    public IList<KindMountModel>? ExtraMounts { get; set; }

    /// <summary>
    /// Gets or sets additional port mappings for the node container.
    /// When <see langword="null"/>, no extra port mappings are configured.
    /// </summary>
    public IList<KindPortMappingModel>? ExtraPortMappings { get; set; }

    /// <summary>
    /// Gets or sets kubeadm config patches applied as merge patches at the node level.
    /// Each entry is a YAML string. Node-level patches are applied after cluster-level patches.
    /// When <see langword="null"/>, no node-level kubeadm patches are applied.
    /// </summary>
    public IList<string>? KubeadmConfigPatches { get; set; }
}

/// <summary>
/// Represents cluster-wide networking settings for a Kind cluster.
/// </summary>
public sealed class KindNetworkingModel
{
    /// <summary>
    /// Gets or sets the network cluster IP family (e.g., "ipv4", "ipv6", or "dual").
    /// When <see langword="null"/>, Kind uses its default (ipv4).
    /// </summary>
    public string? IpFamily { get; set; }

    /// <summary>
    /// Gets or sets the listen port on the host for the Kubernetes API Server.
    /// When <see langword="null"/>, Kind selects a random port.
    /// </summary>
    public int? ApiServerPort { get; set; }

    /// <summary>
    /// Gets or sets the listen address on the host for the Kubernetes API Server.
    /// When <see langword="null"/>, defaults to 127.0.0.1.
    /// </summary>
    public string? ApiServerAddress { get; set; }

    /// <summary>
    /// Gets or sets the CIDR used for pod IPs.
    /// When <see langword="null"/>, Kind selects a default.
    /// </summary>
    public string? PodSubnet { get; set; }

    /// <summary>
    /// Gets or sets the CIDR used for service VIPs.
    /// When <see langword="null"/>, Kind selects a default.
    /// </summary>
    public string? ServiceSubnet { get; set; }

    /// <summary>
    /// Gets or sets whether to disable the default CNI.
    /// Set to <see langword="true"/> to install a custom CNI (e.g., Cilium) after cluster creation.
    /// When <see langword="null"/>, the default CNI is installed.
    /// </summary>
    public bool? DisableDefaultCNI { get; set; }

    /// <summary>
    /// Gets or sets the kube-proxy mode (e.g., "iptables", "ipvs", or "nftables").
    /// When <see langword="null"/>, defaults to "iptables".
    /// </summary>
    public string? KubeProxyMode { get; set; }
}

/// <summary>
/// Represents a host volume mount into a Kind node container.
/// </summary>
public sealed class KindMountModel
{
    /// <summary>
    /// Gets or sets the path of the mount within the container.
    /// </summary>
    public string? ContainerPath { get; set; }

    /// <summary>
    /// Gets or sets the path of the mount on the host.
    /// </summary>
    public string? HostPath { get; set; }

    /// <summary>
    /// Gets or sets whether the mount is read-only.
    /// When <see langword="null"/>, the mount is read-write.
    /// </summary>
    public bool? ReadOnly { get; set; }

    /// <summary>
    /// Gets or sets whether the mount needs SELinux relabeling.
    /// When <see langword="null"/>, no relabeling is performed.
    /// </summary>
    public bool? SelinuxRelabel { get; set; }

    /// <summary>
    /// Gets or sets the mount propagation mode (e.g., "None", "HostToContainer", or "Bidirectional").
    /// When <see langword="null"/>, no propagation is set.
    /// </summary>
    public string? Propagation { get; set; }
}

/// <summary>
/// Represents a host port mapping into a Kind node container.
/// </summary>
public sealed class KindPortMappingModel
{
    /// <summary>
    /// Gets or sets the port within the container.
    /// </summary>
    public int? ContainerPort { get; set; }

    /// <summary>
    /// Gets or sets the port on the host.
    /// When <see langword="null"/>, a random port is selected.
    /// </summary>
    public int? HostPort { get; set; }

    /// <summary>
    /// Gets or sets the address to listen on.
    /// When <see langword="null"/>, Kind uses its default.
    /// </summary>
    public string? ListenAddress { get; set; }

    /// <summary>
    /// Gets or sets the protocol (e.g., "TCP", "UDP", or "SCTP").
    /// When <see langword="null"/>, defaults to TCP.
    /// </summary>
    public string? Protocol { get; set; }
}

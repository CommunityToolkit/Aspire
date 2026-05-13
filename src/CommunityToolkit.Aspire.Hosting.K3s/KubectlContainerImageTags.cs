namespace CommunityToolkit.Aspire.Hosting;

internal static class KubectlContainerImageTags
{
    internal const string Registry = "docker.io";
    // alpine/k8s: lightweight Alpine-based image that includes kubectl and other k8s tools.
    // Same organisation as alpine/helm — consistent image family.
    internal const string Image = "alpine/k8s";
    // Matches the Kubernetes version shipped by the default k3s tag.
    internal const string Tag = "1.32.3";
}

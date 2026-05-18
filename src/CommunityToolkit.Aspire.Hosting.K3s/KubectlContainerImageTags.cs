namespace CommunityToolkit.Aspire.Hosting;

internal static class KubectlContainerImageTags
{
    internal const string Registry = "docker.io";
    // rancher/kubectl: maintained by the same team as k3s. Version tags mirror
    // the Kubernetes version, so v1.32.x pairs correctly with the default k3s tag.
    internal const string Image = "rancher/kubectl";
    internal const string Tag = "v1.32.3";
}

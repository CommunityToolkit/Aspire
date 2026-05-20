namespace CommunityToolkit.Aspire.Hosting;

internal static class KubectlContainerImageTags
{
    internal const string Registry = "docker.io";
    // alpine/kubectl: Alpine-based image with kubectl and /bin/sh, required by the
    // manifest apply script. Tag matches the default k3s server Kubernetes version.
    internal const string Image = "alpine/kubectl";
    internal const string Tag = "1.36.0";
}

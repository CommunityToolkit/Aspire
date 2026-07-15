using CommunityToolkit.Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sClusterOptionsTests
{
    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultsProduceSingleNodeEphemeralCluster()
    {
        var opts = new K3sClusterOptions();

        Assert.Equal(0, opts.AgentCount);
        Assert.Null(opts.ClusterCidr);
        Assert.Null(opts.ServiceCidr);
        Assert.Null(opts.ImageTag);
        Assert.Empty(opts.DisabledComponents);
        Assert.Empty(opts.ExtraArgs);
    }

    [Fact]
    public void HelmImageDefaultsMatchPackageBundledVersion()
    {
        var opts = new K3sClusterOptions();

        Assert.Equal(HelmContainerImageTags.Registry, opts.HelmRegistry);
        Assert.Equal(HelmContainerImageTags.Image, opts.HelmImage);
        Assert.Equal(HelmContainerImageTags.Tag, opts.HelmTag);
    }

    [Fact]
    public void KubectlImageDefaultsMatchPackageBundledVersion()
    {
        var opts = new K3sClusterOptions();

        Assert.Equal(KubectlContainerImageTags.Registry, opts.KubectlRegistry);
        Assert.Equal(KubectlContainerImageTags.Image, opts.KubectlImage);
        Assert.Equal(KubectlContainerImageTags.Tag, opts.KubectlTag);
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanSetAllScalarProperties()
    {
        var opts = new K3sClusterOptions
        {
            AgentCount = 3,
            ClusterCidr = "10.88.0.0/16",
            ServiceCidr = "10.89.0.0/16",
            ImageTag = "v1.32.3-k3s1",
            HelmRegistry = "my.registry.io",
            HelmImage = "my/helm",
            HelmTag = "3.18.0",
            KubectlRegistry = "my.registry.io",
            KubectlImage = "my/kubectl",
            KubectlTag = "1.37.0",
        };

        Assert.Equal(3, opts.AgentCount);
        Assert.Equal("10.88.0.0/16", opts.ClusterCidr);
        Assert.Equal("10.89.0.0/16", opts.ServiceCidr);
        Assert.Equal("v1.32.3-k3s1", opts.ImageTag);
        Assert.Equal("my.registry.io", opts.HelmRegistry);
        Assert.Equal("my/helm", opts.HelmImage);
        Assert.Equal("3.18.0", opts.HelmTag);
        Assert.Equal("my.registry.io", opts.KubectlRegistry);
        Assert.Equal("my/kubectl", opts.KubectlImage);
        Assert.Equal("1.37.0", opts.KubectlTag);
    }

    [Fact]
    public void DisabledComponentsAndExtraArgsAccumulateItems()
    {
        var opts = new K3sClusterOptions();
        opts.DisabledComponents.Add("traefik");
        opts.DisabledComponents.Add("coredns");
        opts.ExtraArgs.Add("--write-kubeconfig-mode=644");

        Assert.Equal(2, opts.DisabledComponents.Count);
        Assert.Single(opts.ExtraArgs);
        Assert.Contains("traefik", opts.DisabledComponents);
        Assert.Contains("--write-kubeconfig-mode=644", opts.ExtraArgs);
    }
}

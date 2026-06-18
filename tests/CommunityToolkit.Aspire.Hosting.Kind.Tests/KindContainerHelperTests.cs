// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using k8s;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KindContainerHelperTests
{
    [Fact]
    public void RewriteClusterEndpointForContainerAccess_RewritesCurrentContextCluster()
    {
        var rewritten = KindContainerHelper.RewriteClusterEndpointForContainerAccess(SampleKubeconfig, "kind-cluster");
        var kubeConfig = LoadKubeConfig(rewritten);

        var clusters = kubeConfig.Clusters.ToDictionary(cluster => cluster.Name, StringComparer.Ordinal);
        var rewrittenCluster = Assert.Contains("kind-kind-cluster", clusters);

        Assert.Equal("https://kind-cluster-control-plane:6443", rewrittenCluster.ClusterEndpoint.Server);
        Assert.True(rewrittenCluster.ClusterEndpoint.SkipTlsVerify);
        Assert.Null(rewrittenCluster.ClusterEndpoint.CertificateAuthorityData);
    }

    [Fact]
    public void RewriteClusterEndpointForContainerAccess_PreservesUnrelatedKubeconfigEntries()
    {
        var rewritten = KindContainerHelper.RewriteClusterEndpointForContainerAccess(SampleKubeconfig, "kind-cluster");
        var kubeConfig = LoadKubeConfig(rewritten);

        var clusters = kubeConfig.Clusters.ToDictionary(cluster => cluster.Name, StringComparer.Ordinal);
        var otherCluster = Assert.Contains("other-cluster", clusters);

        Assert.Equal("https://example.test:6443", otherCluster.ClusterEndpoint.Server);
        Assert.False(otherCluster.ClusterEndpoint.SkipTlsVerify);
        Assert.Equal("b3RoZXItY2E=", otherCluster.ClusterEndpoint.CertificateAuthorityData);
        Assert.Equal("kind-kind-cluster", kubeConfig.CurrentContext);
        Assert.Equal(2, kubeConfig.Contexts.Count());
        Assert.Equal(2, kubeConfig.Users.Count());
    }

    [Fact]
    public void RewriteClusterEndpointForContainerAccess_ThrowsWhenNoClusterMatches()
    {
        const string kubeconfig =
            """
            apiVersion: v1
            kind: Config
            clusters:
            - name: unrelated-cluster
              cluster:
                server: https://example.test:6443
                certificate-authority-data: dGVzdC1jYQ==
            contexts: []
            current-context: ""
            users: []
            preferences: {}
            """;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            KindContainerHelper.RewriteClusterEndpointForContainerAccess(kubeconfig, "my-cluster"));

        Assert.Contains("my-cluster", ex.Message);
    }

    [Fact]
    public void RewriteClusterEndpointForContainerAccess_FallsBackToInferredNameWhenNoCurrentContext()
    {
        const string kubeconfig =
            """
            apiVersion: v1
            kind: Config
            clusters:
            - name: kind-my-cluster
              cluster:
                server: https://127.0.0.1:54321
                certificate-authority-data: dGVzdC1jYQ==
            - name: other-cluster
              cluster:
                server: https://example.test:6443
                certificate-authority-data: b3RoZXItY2E=
            contexts: []
            current-context: ""
            users: []
            preferences: {}
            """;

        var rewritten = KindContainerHelper.RewriteClusterEndpointForContainerAccess(kubeconfig, "my-cluster");
        var kubeConfig = LoadKubeConfig(rewritten);

        var clusters = kubeConfig.Clusters.ToDictionary(cluster => cluster.Name, StringComparer.Ordinal);
        var rewrittenCluster = Assert.Contains("kind-my-cluster", clusters);

        Assert.Equal("https://my-cluster-control-plane:6443", rewrittenCluster.ClusterEndpoint.Server);
        Assert.True(rewrittenCluster.ClusterEndpoint.SkipTlsVerify);

        // Other cluster is untouched
        var otherCluster = Assert.Contains("other-cluster", clusters);
        Assert.Equal("https://example.test:6443", otherCluster.ClusterEndpoint.Server);
    }

    [Fact]
    public void RewriteClusterEndpointForContainerAccess_CurrentContextSelectsCorrectClusterAmongMultiple()
    {
        const string kubeconfig =
            """
            apiVersion: v1
            kind: Config
            clusters:
            - name: kind-alpha
              cluster:
                server: https://127.0.0.1:10001
                certificate-authority-data: YWxwaGE=
            - name: kind-beta
              cluster:
                server: https://127.0.0.1:10002
                certificate-authority-data: YmV0YQ==
            contexts:
            - name: kind-alpha
              context:
                cluster: kind-alpha
                user: alpha-user
            - name: kind-beta
              context:
                cluster: kind-beta
                user: beta-user
            current-context: kind-beta
            users:
            - name: alpha-user
              user:
                token: alpha-token
            - name: beta-user
              user:
                token: beta-token
            preferences: {}
            """;

        var rewritten = KindContainerHelper.RewriteClusterEndpointForContainerAccess(kubeconfig, "beta");
        var kubeConfig = LoadKubeConfig(rewritten);

        var clusters = kubeConfig.Clusters.ToDictionary(cluster => cluster.Name, StringComparer.Ordinal);

        // current-context points to kind-beta, so only that cluster is rewritten
        var betaCluster = Assert.Contains("kind-beta", clusters);
        Assert.Equal("https://beta-control-plane:6443", betaCluster.ClusterEndpoint.Server);
        Assert.True(betaCluster.ClusterEndpoint.SkipTlsVerify);

        // kind-alpha is untouched
        var alphaCluster = Assert.Contains("kind-alpha", clusters);
        Assert.Equal("https://127.0.0.1:10001", alphaCluster.ClusterEndpoint.Server);
        Assert.False(alphaCluster.ClusterEndpoint.SkipTlsVerify);
        Assert.Equal("YWxwaGE=", alphaCluster.ClusterEndpoint.CertificateAuthorityData);
    }

    private static k8s.KubeConfigModels.K8SConfiguration LoadKubeConfig(string kubeconfigYaml)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(kubeconfigYaml));
        return KubernetesClientConfiguration.LoadKubeConfig(stream);
    }

    private const string SampleKubeconfig =
        """
        apiVersion: v1
        kind: Config
        clusters:
        - name: kind-kind-cluster
          cluster:
            server: https://127.0.0.1:42073
            certificate-authority-data: dGVzdC1jYQ==
        - name: other-cluster
          cluster:
            server: https://example.test:6443
            certificate-authority-data: b3RoZXItY2E=
        contexts:
        - name: kind-kind-cluster
          context:
            cluster: kind-kind-cluster
            user: kind-kind-cluster
        - name: other-context
          context:
            cluster: other-cluster
            user: other-user
        current-context: kind-kind-cluster
        users:
        - name: kind-kind-cluster
          user:
            token: kind-token
        - name: other-user
          user:
            token: other-token
        preferences: {}
        """;
}

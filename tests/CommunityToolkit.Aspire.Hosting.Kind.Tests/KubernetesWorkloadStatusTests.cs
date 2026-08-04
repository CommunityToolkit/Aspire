// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KubernetesWorkloadStatusTests
{
    [Fact]
    public void IsReady_DeploymentAllMet_ReturnsTrue()
    {
        var workload = new KubernetesObjectStatus("Deployment", "web", 2, 2, 2, 2);

        Assert.True(workload.IsReady());
    }

    [Fact]
    public void IsReady_DeploymentMissingAvailable_ReturnsFalse()
    {
        var workload = new KubernetesObjectStatus("Deployment", "redis-master", 1, 1, 0, 1);

        Assert.False(workload.IsReady());
    }

    [Fact]
    public void IsReady_DeploymentNoReadyReplicas_ReturnsFalse()
    {
        var workload = new KubernetesObjectStatus("Deployment", "redis-master", 1, 0, 0, 0);

        Assert.False(workload.IsReady());
    }

    [Fact]
    public void IsReady_StatefulSetReadyMet_ReturnsTrue()
    {
        var workload = new KubernetesObjectStatus("StatefulSet", "db", 1, 1, 0, 1);

        Assert.True(workload.IsReady());
    }

    [Fact]
    public void IsReady_StatefulSetNotReady_ReturnsFalse()
    {
        var workload = new KubernetesObjectStatus("StatefulSet", "db", 3, 1, 0, 1);

        Assert.False(workload.IsReady());
    }

    [Fact]
    public void IsReady_UnknownKind_ReturnsFalse()
    {
        var workload = new KubernetesObjectStatus("DaemonSet", "agent", 1, 1, 1, 1);

        Assert.False(workload.IsReady());
    }
}

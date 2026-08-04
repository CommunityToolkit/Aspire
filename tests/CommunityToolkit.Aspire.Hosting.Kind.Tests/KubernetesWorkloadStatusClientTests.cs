// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using k8s.Models;

namespace CommunityToolkit.Aspire.Hosting.Kind.Tests;

public class KubernetesWorkloadStatusClientTests
{
    [Fact]
    public void ToWorkloadStatus_DeploymentPreservesReplicaCounts()
    {
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta { Name = "redis-master" },
            Spec = new V1DeploymentSpec
            {
                Replicas = 3,
                Selector = new V1LabelSelector(),
                Template = new V1PodTemplateSpec(),
            },
            Status = new V1DeploymentStatus
            {
                ReadyReplicas = 2,
                AvailableReplicas = 2,
                UpdatedReplicas = 3,
            },
        };

        var status = KubernetesWorkloadStatusClient.ToWorkloadStatus(deployment);

        Assert.Equal("Deployment", status.Kind);
        Assert.Equal("redis-master", status.Name);
        Assert.Equal(3, status.DesiredReplicas);
        Assert.Equal(2, status.ReadyReplicas);
        Assert.Equal(2, status.AvailableReplicas);
        Assert.Equal(3, status.UpdatedReplicas);
    }

    [Fact]
    public void ToWorkloadStatus_StatefulSetPreservesReplicaCounts()
    {
        var statefulSet = new V1StatefulSet
        {
            Metadata = new V1ObjectMeta { Name = "redis-replicas" },
            Spec = new V1StatefulSetSpec
            {
                Replicas = 3,
                Selector = new V1LabelSelector(),
                ServiceName = "redis",
                Template = new V1PodTemplateSpec(),
            },
            Status = new V1StatefulSetStatus
            {
                ReadyReplicas = 3,
                UpdatedReplicas = 3,
                Replicas = 3,
            },
        };

        var status = KubernetesWorkloadStatusClient.ToWorkloadStatus(statefulSet);

        Assert.Equal("StatefulSet", status.Kind);
        Assert.Equal("redis-replicas", status.Name);
        Assert.Equal(3, status.DesiredReplicas);
        Assert.Equal(3, status.ReadyReplicas);
        Assert.Equal(0, status.AvailableReplicas);
        Assert.Equal(3, status.UpdatedReplicas);
    }

    [Fact]
    public void ToWorkloadStatus_DeploymentDefaultsWhenNull()
    {
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta { Name = "minimal" },
            Spec = new V1DeploymentSpec
            {
                Selector = new V1LabelSelector(),
                Template = new V1PodTemplateSpec(),
            },
            Status = new V1DeploymentStatus(),
        };

        var status = KubernetesWorkloadStatusClient.ToWorkloadStatus(deployment);

        Assert.Equal(1, status.DesiredReplicas);
        Assert.Equal(0, status.ReadyReplicas);
        Assert.Equal(0, status.AvailableReplicas);
        Assert.Equal(0, status.UpdatedReplicas);
    }

    [Fact]
    public void ToWorkloadStatus_StatefulSetDefaultsWhenNull()
    {
        var statefulSet = new V1StatefulSet
        {
            Metadata = new V1ObjectMeta { Name = "minimal" },
            Spec = new V1StatefulSetSpec
            {
                Selector = new V1LabelSelector(),
                ServiceName = "svc",
                Template = new V1PodTemplateSpec(),
            },
            Status = new V1StatefulSetStatus { Replicas = 0 },
        };

        var status = KubernetesWorkloadStatusClient.ToWorkloadStatus(statefulSet);

        Assert.Equal(1, status.DesiredReplicas);
        Assert.Equal(0, status.ReadyReplicas);
        Assert.Equal(0, status.UpdatedReplicas);
    }
}

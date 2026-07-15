using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class K3sInProcessPortForwarderTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static K3sInProcessPortForwarder MakeForwarder(
        List<bool> readyValues,
        Func<string, IKubernetes>? factory = null)
        => new(
            kubeconfigPath: "/fake/kubeconfig.yaml",
            @namespace: "default",
            serviceName: "my-svc",
            localPort: 0,
            servicePort: 80,
            onReadyChanged: v => readyValues.Add(v),
            kubernetesFactory: factory);

    private static Mock<IKubernetes> BuildMockK8s(
        V1Service? service = null,
        V1PodList? pods = null,
        Action? onPodListCalled = null)
    {
        var mockCoreV1 = new Mock<ICoreV1Operations>();
        var mockK8s = new Mock<IKubernetes>();
        mockK8s.Setup(k => k.CoreV1).Returns(mockCoreV1.Object);

        // The source calls the extension method ReadNamespacedServiceAsync which internally
        // calls ReadNamespacedServiceWithHttpMessagesAsync on the interface.
        mockCoreV1
            .Setup(c => c.ReadNamespacedServiceWithHttpMessagesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpOperationResponse<V1Service>
            {
                Body = service ?? MakeService(hasPodSelector: true),
                Response = new HttpResponseMessage(),
            });

        mockCoreV1
            .Setup(c => c.ListNamespacedPodWithHttpMessagesAsync(
                It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<bool?>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                onPodListCalled?.Invoke();
                return new HttpOperationResponse<V1PodList>
                {
                    Body = pods ?? MakePodList(ready: true),
                    Response = new HttpResponseMessage(),
                };
            });

        return mockK8s;
    }

    private static V1Service MakeService(bool hasPodSelector) => new()
    {
        Spec = new V1ServiceSpec
        {
            Ports = [new V1ServicePort { Port = 80, TargetPort = 80 }],
            Selector = hasPodSelector
                ? new Dictionary<string, string> { ["app"] = "my-svc" }
                : null,
        }
    };

    private static V1PodList MakePodList(bool ready) => new()
    {
        Items = [new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = "my-svc-pod-0" },
            Spec = new V1PodSpec
            {
                Containers = [new V1Container { Ports = [new V1ContainerPort { ContainerPort = 80 }] }]
            },
            Status = new V1PodStatus
            {
                Phase = "Running",
                ContainerStatuses = [new V1ContainerStatus { Ready = ready }]
            }
        }]
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_WithoutRunning_CompletesGracefully()
    {
        var forwarder = MakeForwarder([]);
        await forwarder.DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_WithPreCancelledToken_ExitsImmediatelyAndSignalsNotReady()
    {
        var readyValues = new List<bool>();
        var forwarder = MakeForwarder(readyValues);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await forwarder.RunAsync(NullLogger.Instance, cts.Token);

        Assert.Single(readyValues);
        Assert.False(readyValues[0]);
    }

    // ── Ready pod ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenServiceHasReadyPod_SignalsReady()
    {
        var readyValues = new List<bool>();
        using var cts = new CancellationTokenSource();

        // Cancel after WaitForServiceReadyAsync confirms a ready pod, so RunAsync exits cleanly.
        var mockK8s = BuildMockK8s(onPodListCalled: cts.Cancel);

        var forwarder = MakeForwarder(readyValues, _ => mockK8s.Object);
        await forwarder.RunAsync(NullLogger.Instance, cts.Token);

        Assert.Contains(true, readyValues);
        // RunAsync always ends with onReadyChanged(false).
        Assert.False(readyValues[^1]);
    }

    // ── No pod selector ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenServiceHasNoPodSelector_ExitsWithoutSignallingReady()
    {
        var readyValues = new List<bool>();
        var mockK8s = BuildMockK8s(service: MakeService(hasPodSelector: false));

        var forwarder = MakeForwarder(readyValues, _ => mockK8s.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await forwarder.RunAsync(NullLogger.Instance, cts.Token);

        // InvalidOperationException from missing selector causes the loop to break
        // without ever signalling ready.
        Assert.DoesNotContain(true, readyValues);
        Assert.False(readyValues[^1]);
    }

    // ── Not-ready pods ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WhenPodsNotReady_DoesNotSignalReadyBeforeCancellation()
    {
        var readyValues = new List<bool>();
        using var cts = new CancellationTokenSource();

        int callCount = 0;
        var mockK8s = BuildMockK8s(
            pods: MakePodList(ready: false),
            onPodListCalled: () => { if (++callCount >= 2) cts.Cancel(); });

        var forwarder = MakeForwarder(readyValues, _ => mockK8s.Object);
        await forwarder.RunAsync(NullLogger.Instance, cts.Token);

        Assert.DoesNotContain(true, readyValues);
    }
}

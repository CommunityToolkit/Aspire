// K3s hosting example
// ──────────────────────────────────────────────────────────────────────────────
// Prerequisites (host machine):
//   • Docker with --privileged support
//   • helm  → https://helm.sh/docs/intro/install/
//   • kubectl → https://kubernetes.io/docs/tasks/tools/
//
// What this demonstrates:
//   1. A k3s cluster starts inside a Docker container.
//   2. Headlamp (https://headlamp.dev) is installed as a child Helm release —
//      a clickable http URL appears in the Aspire dashboard.
//   3. podinfo is installed — a lightweight demo app that shows the helm lifecycle.
//   4. Both releases are children of k8s in the Aspire resource tree.
//   5. WithPersistentState keeps the cluster data alive across AppHost restarts.
// ──────────────────────────────────────────────────────────────────────────────

var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder.AddK3sCluster("k8s",  configure: opts =>
    {
        opts.AgentCount = 2;
    })
    .WithLifetime(ContainerLifetime.Persistent);

// cluster.AddHelmRelease(
//         name: "podinfo",
//         chart: "podinfo",
//         repo: "https://stefanprodan.github.io/podinfo",
//         version: "6.7.1",
//         @namespace: "podinfo")
//     .WithHelmValue("service.type", "NodePort")
//     .WithEndpoint("podinfo", servicePort: 9898, name: "web");

builder.Build().Run();

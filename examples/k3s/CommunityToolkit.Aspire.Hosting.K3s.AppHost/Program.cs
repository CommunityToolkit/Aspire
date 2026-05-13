// K3s hosting example
// ──────────────────────────────────────────────────────────────────────────────
// Prerequisites (host machine):
//   • Docker with --privileged support (Linux or Docker Desktop on Mac/Windows)
//   • helm  → https://helm.sh/docs/intro/install/
//   • kubectl → https://kubernetes.io/docs/tasks/tools/
//
// What this demonstrates:
//   1. A k3s cluster starts inside a Docker container.
//   2. podinfo is installed via Helm — a lightweight demo app.
//   3. A K3sServiceEndpointResource exposes the podinfo service:
//        • Host processes reach it at http://localhost:{port}
//        • DCP-network containers reach it at http://host.docker.internal:{port}
//   4. WithDataVolume keeps the cluster state alive across AppHost restarts.
// ──────────────────────────────────────────────────────────────────────────────

var builder = DistributedApplication.CreateBuilder(args);

var cluster = builder
    .AddK3sCluster("k8s")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var podinfo = cluster.AddHelmRelease(
    name: "podinfo",
    chart: "podinfo",
    repo: "https://stefanprodan.github.io/podinfo",
    version: "6.7.1",
    @namespace: "podinfo");

// Expose the podinfo service as an Aspire endpoint resource.
// WaitForCompletion waits for the helm install container to exit with code 0
// before starting the port-forward — no NodePort required.
cluster.AddServiceEndpoint("podinfo-web", "podinfo", servicePort: 9898, @namespace: "podinfo")
    .WaitForCompletion(podinfo);

builder.Build().Run();

import { createBuilder, ContainerLifetime } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();

// ── Runtime path (actually executed) ─────────────────────────────────────────
// Single-node cluster: installs podinfo via Helm and exposes the service as an
// Aspire endpoint — validating the full add/build/run path. No agent nodes so
// the CI runner has enough CPU/RAM to schedule pods. withAgentCount is covered
// by the compile-time section below.
const cluster = await builder.addK3sCluster('k8s');

const _apiEndpoint = await cluster.apiEndpoint();

const setupPodinfo = await cluster.addHelmRelease('podinfo', 'podinfo', {
    repo: 'https://stefanprodan.github.io/podinfo',
    version: '6.7.1',
    namespace: 'podinfo',
});

await cluster.addServiceEndpoint('podinfo-web', 'podinfo', 9898, {
    namespace: 'podinfo',
}).waitForCompletion(setupPodinfo);

// ── Compile-time coverage ─────────────────────────────────────────────────────
// Guards with false so these are type-checked but never executed.
// Covers the full exported API surface without requiring Docker/k3s in CI.
const includeCompileOnlyScenarios = false;

if (includeCompileOnlyScenarios) {

    // ── Cluster configuration ────────────────────────────────────────────────
    // All K3sClusterOptions are now available as fluent builder methods.
    const configuredCluster = await builder.addK3sCluster('k8s-configured', {
            apiServerPort: 6443,
            agentCount: 2
        })
        .withK3sVersion('v1.32.3-k3s1')
        .withPodSubnet('10.42.0.0/16')
        .withServiceSubnet('10.43.0.0/16')
        .withDisabledComponent('traefik')
        .withExtraArg('--write-kubeconfig-mode=644')
        .withDataVolume({ name: 'k8s-data' })
        .withHelmImage({ tag: '3.18.0' })
        .withKubectlImage({ tag: '1.37.0' })
        .withLifetime(ContainerLifetime.Persistent);

    const _configuredApiEndpoint = await configuredCluster.apiEndpoint();

    // ── Helm release — podinfo ────────────────────────────────────────────────
    const podinfo = await configuredCluster.addHelmRelease('podinfo', 'podinfo', {
        repo: 'https://stefanprodan.github.io/podinfo',
        version: '6.7.1',
        namespace: 'podinfo',
    });

    const _podinfoParent = await podinfo.parent();
    const _podinfoReleaseName = await podinfo.releaseName();
    const _podinfoNamespace = await podinfo.namespace();

    // ── K8s manifest — plain YAML file ───────────────────────────────────────
    const appConfig = await configuredCluster
        .addK8sManifest('app-config', './k8s/app-config.yaml')
        .waitForCompletion(podinfo);

    const _appConfigParent = await appConfig.parent();
    const _appConfigPath = await appConfig.path();

    // ── K8s manifest — Kustomize overlay ─────────────────────────────────────
    // Auto-detected because the directory contains kustomization.yaml.
    const monitoringConfig = await configuredCluster
        .addK8sManifest('monitoring-config', './k8s/monitoring')
        .waitForCompletion(podinfo)
        .waitForCompletion(appConfig);

    const _monitoringParent = await monitoringConfig.parent();

    // ── Service endpoint ─────────────────────────────────────────────────────
    //   • Host processes receive  services__podinfo-web__url=http://localhost:{port}
    //   • DCP containers receive  services__podinfo-web__url=http://host.docker.internal:{port}
    const podinfoWeb = await configuredCluster
        .addServiceEndpoint('podinfo-web', 'podinfo', 9898, {
            namespace: 'podinfo',
        })
        .waitForCompletion(podinfo)
        .waitForCompletion(monitoringConfig);

    const _webParent = await podinfoWeb.parent();
    const _webServiceName = await podinfoWeb.serviceName();
    const _webServicePort = await podinfoWeb.servicePort();
    const _webNamespace = await podinfoWeb.namespace();
    const _webHostPort = await podinfoWeb.hostPort.get();

    // ── WithReference — cluster kubeconfig injection ──────────────────────────
    //   • Project/executable: KUBECONFIG=<host>/.k3s/k8s/local/kubeconfig.yaml
    //   • Container: KUBECONFIG=/tmp/k3s-kubeconfig.yaml + bind-mount
    const _projectRef = await builder
        .addProject('operator', '../WidgetOperator/WidgetOperator.csproj')
        .withReference(configuredCluster);

    const _containerRef = await builder
        .addContainer('sidecar', 'myorg/sidecar')
        .withReference(configuredCluster);

    // ── WithReference — service endpoint URL injection ────────────────────────
    const _endpointRef = await builder
        .addProject('api', '../WidgetApi/WidgetApi.csproj')
        .withReference(podinfoWeb);
}

await builder.build().run();

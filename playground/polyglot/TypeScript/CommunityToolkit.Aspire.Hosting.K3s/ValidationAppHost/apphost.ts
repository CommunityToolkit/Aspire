import { createBuilder, ContainerLifetime } from './.modules/aspire.js';

const builder = await createBuilder();

// ── Runtime path (actually executed) ─────────────────────────────────────────
// Minimal cluster startup — validates that the core add/build/run path works.
const cluster = builder.addK3sCluster('k8s');
const clusterResource = await cluster;
const _apiEndpoint = await clusterResource.apiEndpoint.get();

// ── Compile-time coverage ─────────────────────────────────────────────────────
// Guards with false so these are type-checked but never executed.
// Covers the full exported API surface without requiring Docker/k3s in CI.
const includeCompileOnlyScenarios = false;

if (includeCompileOnlyScenarios) {

    // ── Cluster configuration ────────────────────────────────────────────────
    const configuredCluster = builder.addK3sCluster('k8s-configured')
        .withK3sVersion('v1.32.3-k3s1')
        .withPodSubnet('10.42.0.0/16')
        .withServiceSubnet('10.43.0.0/16')
        .withDisabledComponent('traefik')
        .withExtraArg('--write-kubeconfig-mode=644')
        .withDataVolume({ name: 'k8s-data' })
        .withLifetime(ContainerLifetime.Persistent);

    const configuredClusterResource = await configuredCluster;
    const _configuredApiEndpoint = await configuredClusterResource.apiEndpoint.get();

    // ── Helm release ─────────────────────────────────────────────────────────
    const argocd = configuredCluster.addHelmRelease('argocd', 'argo-cd', {
        repo: 'https://argoproj.github.io/argo-helm',
        version: '7.8.0',
        namespace: 'argocd',
    })
        .withHelmValue('server.insecure', 'true')
        .withHelmValuesFile('./deploy/argocd-values.yaml');

    const argocdResource = await argocd;
    const _argocdParent = await argocdResource.parent.get();
    const _argocdReleaseName = await argocdResource.releaseName.get();
    const _argocdNamespace = await argocdResource.namespace.get();

    // ── K8s manifest / Kustomize overlay ─────────────────────────────────────
    const widgetCrd = configuredCluster.addK8sManifest('widget-crd', './k8s/crds/');

    const widgetCrdResource = await widgetCrd;
    const _crdParent = await widgetCrdResource.parent.get();
    const _crdPath = await widgetCrdResource.path.get();

    // ── Service endpoint ─────────────────────────────────────────────────────
    const ui = configuredCluster.addServiceEndpoint('argocd-ui', 'argocd-server', 443, {
        namespace: 'argocd',
    });

    const uiResource = await ui;
    const _uiParent = await uiResource.parent.get();
    const _uiServiceName = await uiResource.serviceName.get();
    const _uiServicePort = await uiResource.servicePort.get();
    const _uiNamespace = await uiResource.namespace.get();
    const _uiHostPort = await uiResource.hostPort.get();

    // ── WithReference — cluster kubeconfig injection ──────────────────────────
    // Project: receives KUBECONFIG=.../.k3s/k8s/local/kubeconfig.yaml
    const _projectRef = builder
        .addProject('operator', { projectPath: '../WidgetOperator/WidgetOperator.csproj' })
        .withReference(configuredCluster);

    // Container: receives KUBECONFIG_DATA=<base64>
    const _containerRef = builder
        .addContainer('sidecar', 'myorg/sidecar')
        .withReference(configuredCluster);

    // ── WithReference — service endpoint URL injection ────────────────────────
    // Host: receives services__argocd-ui__url=https://localhost:{port}
    const _endpointRef = builder
        .addProject('api', { projectPath: '../WidgetApi/WidgetApi.csproj' })
        .withReference(ui);
}

await builder.build().run();

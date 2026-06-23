// Aspire Go AppHost
// For more information, see: https://aspire.dev

package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	// ── Runtime path (actually executed) ─────────────────────────────────────────
	// Single-node cluster: installs podinfo via Helm and exposes the service as an
	// Aspire endpoint — validating the full add/build/run path. No agent nodes so
	// the CI runner has enough CPU/RAM to schedule pods. withAgentCount is covered
	// by the compile-time section below.
	cluster := builder.AddK3sCluster("k8s")

	_ = cluster.ApiEndpoint()

	setupPodinfo := cluster.AddHelmRelease("podinfo", "podinfo", &aspire.AddHelmReleaseOptions{
		Repo:      aspire.StringPtr("https://stefanprodan.github.io/podinfo"),
		Version:   aspire.StringPtr("6.7.1"),
		Namespace: aspire.StringPtr("podinfo"),
	})

	cluster.AddServiceEndpoint("podinfo-web", "podinfo", 9898, &aspire.AddServiceEndpointOptions{
		Namespace: aspire.StringPtr("podinfo"),
	}).WaitForCompletion(setupPodinfo)

	// ── Compile-time coverage ─────────────────────────────────────────────────────
	// Guards with false so these are type-checked but never executed.
	// Covers the full exported API surface without requiring Docker/k3s in CI.
	const includeCompileOnlyScenarios = false

	if (includeCompileOnlyScenarios) {
		// ── Cluster configuration ────────────────────────────────────────────────
		// All K3sClusterOptions are now available as fluent builder methods.
		configuredCluster := builder.AddK3sCluster("k8s-configured", 
			&aspire.AddK3sClusterOptions{
				ApiServerPort: aspire.Float64Ptr(6443),
				AgentCount: aspire.Float64Ptr(2),
			}).
			WithK3sVersion("v1.32.3-k3s1").
			WithPodSubnet("10.42.0.0/16").
			WithServiceSubnet("10.43.0.0/16").
			WithDisabledComponent("traefik").
			WithExtraArg("--write-kubeconfig-mode=644").
			WithDataVolume(&aspire.WithDataVolumeOptions{ Name: aspire.StringPtr("k8s-data") }).
			WithHelmImage(&aspire.WithHelmImageOptions{ Tag: aspire.StringPtr("3.18.0") }).
			WithKubectlImage(&aspire.WithKubectlImageOptions{ Tag: aspire.StringPtr("1.37.0") }).
			WithLifetime(aspire.ContainerLifetimePersistent)

		_ = configuredCluster.ApiEndpoint()

		// ── Helm release — podinfo ────────────────────────────────────────────────
		podinfo := configuredCluster.AddHelmRelease("podinfo", "podinfo", &aspire.AddHelmReleaseOptions{
			Repo:      aspire.StringPtr("https://stefanprodan.github.io/podinfo"),
			Version:   aspire.StringPtr("6.7.1"),
			Namespace: aspire.StringPtr("podinfo"),
		})

		_ = podinfo.Parent()
		
		_, _ = podinfo.ReleaseName()
		_, _ = podinfo.Namespace()

		// ── K8s manifest — plain YAML file ───────────────────────────────────────
		appConfig := configuredCluster.
			AddK8sManifest("app-config", "./k8s/app-config.yaml").
			WaitForCompletion(podinfo)

		_ = appConfig.Parent()
		_, _ = appConfig.Path()

		// ── K8s manifest — Kustomize overlay ─────────────────────────────────────
		// Auto-detected because the directory contains kustomization.yaml.
		monitoringConfig := configuredCluster.
			AddK8sManifest("monitoring-config", "./k8s/monitoring").
			WaitForCompletion(podinfo).
			WaitForCompletion(appConfig)

		_ = monitoringConfig.Parent()

		// ── Service endpoint ─────────────────────────────────────────────────────
		//   • Host processes receive  services__podinfo-web__url=http://localhost:{port}
		//   • DCP containers receive  services__podinfo-web__url=http://host.docker.internal:{port}
		podinfoWeb := configuredCluster.
			AddServiceEndpoint("podinfo-web", "podinfo", 9898, &aspire.AddServiceEndpointOptions{
				Namespace: aspire.StringPtr("podinfo"),
			}).
			WaitForCompletion(podinfo).
			WaitForCompletion(monitoringConfig)

		_ = podinfoWeb.Parent()
		_, _ = podinfoWeb.ServiceName()
		_, _ = podinfoWeb.ServicePort()
		_, _ = podinfoWeb.Namespace()
		_, _ = podinfoWeb.HostPort()

		// ── WithReference — cluster kubeconfig injection ──────────────────────────
		//   • Project/executable: KUBECONFIG=<host>/.k3s/k8s/local/kubeconfig.yaml
		//   • Container: KUBECONFIG=/tmp/k3s-kubeconfig.yaml + bind-mount
		_ = builder.
			AddProject("operator", "../WidgetOperator/WidgetOperator.csproj").
			WithReference(configuredCluster)

		_ = builder.
			AddContainer("sidecar", "myorg/sidecar").
			WithReference(configuredCluster)

		// ── WithReference — service endpoint URL injection ────────────────────────
		
		_ = builder.
			AddProject("api", "../WidgetApi/WidgetApi.csproj").
			WithReference(podinfoWeb)
	}

	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}
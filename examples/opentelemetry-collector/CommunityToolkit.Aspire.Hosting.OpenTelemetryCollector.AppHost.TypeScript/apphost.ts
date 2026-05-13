import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addOpenTelemetryCollector — default overload
const collector = await builder.addOpenTelemetryCollector("collector");
await collector.withConfig("./otel-config.yaml");
await collector.withAppForwarding();

// addOpenTelemetryCollector — settings callback overload
const routedCollector = await builder.addOpenTelemetryCollector("collector-routed", {
    configureSettings: async (settings) => {
        await settings.forceNonSecureReceiver.set(true);
        await settings.enableGrpcEndpoint.set(true);
        await settings.enableHttpEndpoint.set(true);
        await settings.disableHealthcheck.set(false);
        await settings.registry.set("ghcr.io");
        await settings.image.set("open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib");
        await settings.collectorTag.set("latest");

        const _forceNonSecureReceiver: boolean = await settings.forceNonSecureReceiver.get();
        const _collectorTag: string = await settings.collectorTag.get();
        const _collectorImage: string = await settings.collectorImage.get();
    }
});

await routedCollector.withConfig("./otel-config.yaml");
await routedCollector.withOpenTelemetryCollectorRouting(collector);

// ---- Property access on OpenTelemetryCollectorResource (ExposeProperties = true) ----
const collectorResource = await collector;
const _grpcEndpoint = await collectorResource.grpcEndpoint.get();
const _httpEndpoint = await collectorResource.httpEndpoint.get();
const _grpcEndpointName = await _grpcEndpoint.endpointName.get();
const _httpEndpointName = await _httpEndpoint.endpointName.get();

const routedCollectorResource = await routedCollector;
const _routedGrpcEndpoint = await routedCollectorResource.grpcEndpoint.get();
const _routedHttpEndpoint = await routedCollectorResource.httpEndpoint.get();
const _routedGrpcEndpointName = await _routedGrpcEndpoint.endpointName.get();
const _routedHttpEndpointName = await _routedHttpEndpoint.endpointName.get();

await builder.build().run();

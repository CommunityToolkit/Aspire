import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

// addOpenTelemetryCollector — settings overload
const collector = await builder.addOpenTelemetryCollector("collector", {
    configureSettings: async settings => {
        // This TypeScript smoke validates settings export/startup; C# tests cover the default health endpoint.
        await settings.disableHealthcheck.set(true);
    },
});
await collector.withConfig("./otel-config.yaml");
await collector.withAppForwarding();

// ---- Property access on OpenTelemetryCollectorResource (ExposeProperties = true) ----
const _grpcEndpoint = await collector.grpcEndpoint();
const _httpEndpoint = await collector.httpEndpoint();
const _grpcEndpointName = await _grpcEndpoint.endpointName();
const _httpEndpointName = await _httpEndpoint.endpointName();

await builder.build().run();

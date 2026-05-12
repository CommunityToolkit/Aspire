import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

// addOpenTelemetryCollector — default overload
const collector = await builder.addOpenTelemetryCollector("collector");
await collector.withConfig("./otel-config.yaml");
await collector.withAppForwarding();

// ---- Property access on OpenTelemetryCollectorResource (ExposeProperties = true) ----
const _grpcEndpoint = await collector.grpcEndpoint.get();
const _httpEndpoint = await collector.httpEndpoint.get();
const _grpcEndpointName = await _grpcEndpoint.endpointName.get();
const _httpEndpointName = await _httpEndpoint.endpointName.get();

await builder.build().run();

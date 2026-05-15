import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

// addOpenTelemetryCollector — default overload
const collector = await builder.addOpenTelemetryCollector("collector");
await collector.withConfig("./otel-config.yaml");
await collector.withAppForwarding();

// ---- Property access on OpenTelemetryCollectorResource (ExposeProperties = true) ----
const _grpcEndpoint = await collector.grpcEndpoint();
const _httpEndpoint = await collector.httpEndpoint();
const _grpcEndpointName = await _grpcEndpoint.endpointName();
const _httpEndpointName = await _httpEndpoint.endpointName();

await builder.build().run();

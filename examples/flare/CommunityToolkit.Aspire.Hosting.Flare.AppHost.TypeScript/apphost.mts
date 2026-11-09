import { createBuilder, OtlpProtocol } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const clickhouse = await builder.addClickHouse("clickhouse");
const redis = await builder.addRedis("redis");

const ingestKey = await builder.addParameter("flare-ingest-key", {
    value: "local-dev-ingest-key",
    secret: true,
});
const dashboardUrl = await builder.addParameter("flare-dashboard-url", {
    value: "http://localhost:3000",
});

// addFlare: ClickHouse and Redis are passed in, the dashboard gets a fixed host port
const flare = await builder.addFlare("flare", clickhouse, redis, { dashboardPort: 3000 });

await flare.withDataVolume();
await flare.withIngestPorts({ grpcPort: 4317, httpPort: 4318 });
await flare.withFlareImageTag("0.6");
await flare.withPublicUrls({ dashboardUrl });

// withApiKey: OTLP clients must send the key as "Authorization: Bearer <key>";
// withFlareOtlpExporter below adds that header automatically
await flare.withApiKey(ingestKey);

// Property access on FlareResource (ExposeProperties = true)
const _otlpGrpcEndpoint = await flare.otlpGrpcEndpoint();
const _otlpHttpEndpoint = await flare.otlpHttpEndpoint();

// withFlareOtlpExporter: the example service sends its logs, traces and metrics to Flare
const apiService = await builder.addDotnetProject(
    "apiservice",
    "../CommunityToolkit.Aspire.Hosting.Flare.ApiService/CommunityToolkit.Aspire.Hosting.Flare.ApiService.csproj",
);
await apiService.withFlareOtlpExporter(flare, { protocol: OtlpProtocol.HttpProtobuf });
await apiService.waitFor(flare);

await builder.build().run();

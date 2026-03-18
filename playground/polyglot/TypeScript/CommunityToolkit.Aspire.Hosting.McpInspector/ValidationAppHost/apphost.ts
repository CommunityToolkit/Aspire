import { createBuilder, McpTransportType } from './.modules/aspire.js';

const builder = await createBuilder();

const proxyToken = await builder.addParameter("proxy-token", { secret: true });

const inspectedServer = await builder
    .addContainer("inspected-server", "nginx:alpine")
    .withHttpEndpoint({ name: "http", targetPort: 80 });

const secondaryServer = await builder
    .addContainer("secondary-server", "nginx:alpine")
    .withHttpEndpoint({ name: "http", targetPort: 80 });

const inspectorDefault = await builder.addMcpInspector("inspector-default");
await inspectorDefault.withInspectedMcpServer(inspectedServer);

const inspectorConfigured = await builder.addMcpInspector("inspector-configured", {
    clientPort: 6284,
    serverPort: 6287,
    inspectorVersion: "0.17.2",
    proxyToken
});
await inspectorConfigured.withInspectedMcpServer(secondaryServer, {
    isDefault: false,
    transportType: McpTransportType.StreamableHttp,
    path: "/custom-mcp"
});

const inspectorYarn = await builder.addMcpInspector("inspector-yarn", {
    clientPort: 6294,
    serverPort: 6297
});
await inspectorYarn.withYarn();

const inspectorPnpm = await builder.addMcpInspector("inspector-pnpm", {
    clientPort: 6304,
    serverPort: 6307
});
await inspectorPnpm.withPnpm();

const inspectorBun = await builder.addMcpInspector("inspector-bun", {
    clientPort: 6314,
    serverPort: 6317
});
await inspectorBun.withBun();

await builder.build().run();

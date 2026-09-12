import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const vercel = await builder.addVercelEnvironment("vercel");
await vercel.withVercelProductionDeployments();
await builder.addDockerComposeEnvironment("docker");

const api = await builder
    .addNodeApp("api", "./ct-aspire-vercel-typescript", "server.mjs");

await api.withVercelProjectName("typescript-api");
await api.withComputeEnvironment(vercel);
await api.withEnvironment("GREETING", "hello-from-typescript-apphost");

await builder.build().run();

import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const vercel = await builder.addVercelEnvironment("vercel");
await vercel.withVercelCliPath("vercel");
await vercel.withVercelProductionDeployments();

const api = await builder
    .addNodeApp("api", "./ct-aspire-vercel-typescript", "server.mjs");

await api.withEnvironment("GREETING", "hello-from-typescript-apphost");

await builder.build().run();

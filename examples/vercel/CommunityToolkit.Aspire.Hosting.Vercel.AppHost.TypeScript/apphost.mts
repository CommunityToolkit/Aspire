import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const vercel = await builder.addVercelEnvironment("vercel");
await vercel.withVercelCliPath("vercel");
await vercel.withVercelProductionDeployments();

const api = await builder
    .addContainer("api", "ct-aspire-vercel-typescript")
    .withDockerfile("./ct-aspire-vercel-typescript", { dockerfilePath: "Dockerfile.vercel" });

await api.withEnvironment("GREETING", "hello-from-typescript-apphost");
await api.publishAsVercel(vercel);

await builder.build().run();

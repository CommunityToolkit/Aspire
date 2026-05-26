import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

await builder
    .addDenoTask("vite-demo", {
        taskName: "dev"
    })
    .withDenoPackageInstallation()
    .withHttpEndpoint({ env: "PORT" })
    .withEndpoint()
    .withHttpHealthCheck({ path: "/" });

await builder
    .addDenoApp("oak-demo", "main.ts", {
        permissionFlags: ["--allow-env", "--allow-net"]
    })
    .withHttpEndpoint({ env: "PORT" })
    .withEndpoint()
    .withHttpHealthCheck({ path: "/health" });

await builder.build().run();

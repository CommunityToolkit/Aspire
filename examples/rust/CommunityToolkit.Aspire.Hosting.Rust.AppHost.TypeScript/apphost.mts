import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const rustAppPath = "../actix_api";

// addRustApp — minimal call
const rustApp = await builder.addRustApp("rust-app", rustAppPath);
await rustApp.withHttpEndpoint({ env: "PORT" });
await rustApp.withExternalHttpEndpoints();
await rustApp.withHttpHealthCheck({ path: "/" });

if (process.env["ENABLE_BACON"] === "1") {
    const baconApp = await builder.addBaconApp("bacon-app", rustAppPath);
    await baconApp.withHttpEndpoint({ env: "PORT" });
}

await builder.build().run();


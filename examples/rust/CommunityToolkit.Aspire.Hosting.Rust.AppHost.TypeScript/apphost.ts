import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const rustAppPath = "../../../../../examples/rust/actix_api";

// addRustApp — minimal call
const rustApp = await builder.addRustApp("rust-app", rustAppPath);
await rustApp.withHttpEndpoint({ env: "PORT" });
await rustApp.withExternalHttpEndpoints();
await rustApp.withHttpHealthCheck({ path: "/" });

// addRustApp — optional args
if (process.env["RUST_ARGS_VALIDATION"] === "1")
{
    const rustAppWithArgs = await builder.addRustApp("rust-app-with-args", rustAppPath, { args: ["--", "--help"] });
    await rustAppWithArgs.withHttpEndpoint({ env: "PORT" });
}

await builder.build().run();

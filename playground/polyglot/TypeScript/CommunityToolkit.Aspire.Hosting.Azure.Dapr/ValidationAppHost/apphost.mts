import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const environment = await builder.addAzureContainerAppEnvironment("cae");
await environment.withDaprComponents();

await builder.build().run();


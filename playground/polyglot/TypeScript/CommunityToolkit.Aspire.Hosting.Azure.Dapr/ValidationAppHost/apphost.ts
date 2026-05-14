import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const environment = await builder.addAzureContainerAppEnvironment("cae");
await environment.withDaprComponents();

await builder.build().run();

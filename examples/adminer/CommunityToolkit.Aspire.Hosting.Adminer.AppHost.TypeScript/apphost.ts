import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const adminer = await builder.addAdminer("adminer", { port: 18080 });
await adminer.withHostPort({ port: 18081 });

await builder.build().run();

import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const loki = await builder.addLoki("loki", {
	httpPort: 13100,
	grpcPort: 19096,
});

const _httpEndpoint = await loki.primaryEndpoint();
const _grpcEndpoint = await loki.grpcEndpoint();

await builder.build().run();
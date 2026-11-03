import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const postgres = await builder.addPostgres("postgres");
const redis = await builder.addRedis("redis");

const logto = await builder.addLogto("logto", postgres);
await logto.withRedis(redis);
await logto.withDatabaseSeeding();

const clientOIDC = await builder.addDotnetProject(
    "clientOIDC",
    "../CommunityToolkit.Aspire.Logto.ClientOIDC/CommunityToolkit.Aspire.Logto.ClientOIDC.csproj",
);
await clientOIDC.withReference(logto);
await clientOIDC.waitFor(logto);

const clientJWT = await builder.addDotnetProject(
    "clientJWT",
    "../CommunityToolkit.Aspire.Logto.ClientJWT/CommunityToolkit.Aspire.Logto.ClientJWT.csproj",
);
await clientJWT.withReference(logto);
await clientJWT.waitFor(logto);

await builder.build().run();

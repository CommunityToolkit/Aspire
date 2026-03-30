import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const umamiSecret = await builder.addParameterWithValue("umami-secret", "SuperSecret123!", { secret: true });
const postgres = await builder.addPostgres("postgres");
const analyticsDatabase = await postgres.addDatabase("analytics");

// addUmami — with explicit secret and port
const umami = await builder.addUmami("umami", {
    secret: umamiSecret,
    port: 31300
});

// addUmami — minimal overload
const umamiDefault = await builder.addUmami("umami-default");

// withPostgreSQL — configure the PostgreSQL backend
await umami.withPostgreSQL(analyticsDatabase);
await umamiDefault.withPostgreSQL(analyticsDatabase);

// Property access on UmamiResource (ExposeProperties = true)
const umamiResource = await umami;
const _primaryEndpoint = await umamiResource.primaryEndpoint.get();

await builder.build().run();

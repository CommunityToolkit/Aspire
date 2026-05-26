import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const username = await builder.addParameter("zitadel-username", { value: "admin" });
const password = await builder.addParameter("zitadel-password", { value: "AspireZitadelPassword!1", secret: true });
const masterKey = await builder.addParameter("zitadel-master-key", { value: "Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!Aa1!", secret: true });

const postgres = await builder.addPostgres("postgres");
const existingDatabase = await postgres.addDatabase("zitadel-existing-db");

const zitadel = await builder.addZitadel("zitadel", {
    port: 8080,
    username,
    password,
    masterKey
});

await zitadel.withExternalDomain("auth.example.com");
await zitadel.withDatabase(postgres, { databaseName: "zitadel-db" });

const minimalZitadel = await builder.addZitadel("zitadel-minimal");
await minimalZitadel.withExistingDatabase(existingDatabase);
await minimalZitadel.withExternalDomain("login.example.com");

await builder.build().run();

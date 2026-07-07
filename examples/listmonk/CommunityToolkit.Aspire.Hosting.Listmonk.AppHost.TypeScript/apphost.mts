import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const password = await builder.addParameter("db-password", {
    value: "12345678",
    secret: true,
});
const adminPassword = await builder.addParameter("admin-password", {
    value: "SuperSecret123!",
    secret: true,
});
const postgres = await builder.addPostgres("postgres", { password });
const listmonkDb = await postgres.addDatabase("listmonkdb");

const listmonk = await builder.addListmonk("listmonk", {
    port: 31900,
});
const listmonkDefault = await builder.addListmonk("listmonk-default");

await listmonk.withPostgreSQL(listmonkDb);
await listmonk.withAdminCredentials("admin", adminPassword);
await listmonk.withDatabaseMaxOpenConnections(25);
await listmonk.withDatabaseMaxIdleConnections(25);
await listmonk.withDatabaseMaxLifetime("300s");
await listmonk.withTimeZone("Etc/UTC");
await listmonk.withUserAndGroupId(0, 0);
await listmonk.withUploadsVolume();
await listmonkDefault.withPostgreSQL(listmonkDb);
await listmonkDefault.withDatabaseSslMode("disable");

const _primaryEndpoint = await listmonk.primaryEndpoint();
const _host = await listmonk.host();
const _port = await listmonk.port();
const _uriExpression = await listmonk.uriExpression();
const _connectionStringExpression = await listmonk.connectionStringExpression();

await builder.build().run();

import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const adminPassword = await builder.addParameter("admin-password", {
    value: "SuperSecret123!",
    secret: true,
});

const listmonk = await builder.addListmonk("listmonk", {
    port: 31900,
    postgresName: "postgres",
    databaseName: "listmonkdb",
});
const listmonkDefault = await builder.addListmonk("listmonk-default");

await listmonk.withAdminCredentials("admin", adminPassword);
await listmonk.withDatabaseMaxOpenConnections(25);
await listmonk.withDatabaseMaxIdleConnections(25);
await listmonk.withDatabaseMaxLifetime("300s");
await listmonk.withTimeZone("Etc/UTC");
await listmonk.withUserId(0);
await listmonk.withGroupId(0);
await listmonk.withUploadsVolume();
await listmonkDefault.withDatabaseSslMode("disable");

const _primaryEndpoint = await listmonk.primaryEndpoint();
const _host = await listmonk.host();
const _port = await listmonk.port();
const _uriExpression = await listmonk.uriExpression();
const _connectionStringExpression = await listmonk.connectionStringExpression();

await builder.build().run();

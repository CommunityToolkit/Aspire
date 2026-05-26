import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

const postgres = await builder.addPostgres("postgres");
await postgres.withDbGate({ containerName: "postgres-dbgate" });
await postgres.withAdminer({ containerName: "postgres-adminer" });

const database = await postgres.addDatabase("appdb");
await database.withFlywayMigration("flyway-migration", "./migrations");
await database.withFlywayRepair("flyway-repair", "./migrations");

const _primaryEndpoint = await postgres.primaryEndpoint();
const _host = await postgres.host();
const _port = await postgres.port();
const _serverUri = await postgres.uriExpression();
const _serverJdbcConnectionString = await postgres.jdbcConnectionString();

const _databaseName = await database.databaseName();
const _databaseUri = await database.uriExpression();
const _databaseJdbcConnectionString = await database.jdbcConnectionString();

await builder.build().run();

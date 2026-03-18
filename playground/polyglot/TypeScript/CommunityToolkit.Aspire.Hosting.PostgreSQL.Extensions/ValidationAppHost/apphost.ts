import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const postgres = await builder.addPostgres('postgres');
await postgres.withDbGate({ containerName: 'postgres-dbgate' });
await postgres.withAdminer({ containerName: 'postgres-adminer' });

const database = await postgres.addDatabase('appdb');
await database.withFlywayMigration('flyway-migration', './migrations');
await database.withFlywayRepair('flyway-repair', './migrations');

const resolvedPostgres = await postgres;
const _primaryEndpoint = await resolvedPostgres.primaryEndpoint.get();
const _host = await resolvedPostgres.host.get();
const _port = await resolvedPostgres.port.get();
const _serverUri = await resolvedPostgres.uriExpression.get();
const _serverJdbcConnectionString = await resolvedPostgres.jdbcConnectionString.get();

const resolvedDatabase = await database;
const _databaseName = await resolvedDatabase.databaseName.get();
const _databaseUri = await resolvedDatabase.uriExpression.get();
const _databaseJdbcConnectionString = await resolvedDatabase.jdbcConnectionString.get();

await builder.build().run();

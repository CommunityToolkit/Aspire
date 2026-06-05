import { createBuilder, SqliteWebResource } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const sqlite = await builder.addSqlite("sqlite", {
    databasePath: ".data/sqlite",
    databaseFileName: "app.db",
});
const sqliteDefault = await builder.addSqlite("sqlite-default");

await sqlite.withSqliteWeb({ containerName: "sqlite-browser" });
await sqliteDefault.withSqliteWeb();

const _sqliteConnectionString = await sqlite.connectionStringExpression();

await builder.build().run();


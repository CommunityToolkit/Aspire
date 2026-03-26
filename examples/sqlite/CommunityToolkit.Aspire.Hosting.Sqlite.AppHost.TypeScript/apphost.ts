import { createBuilder, SqliteWebResource } from './.modules/aspire.js';

function asSqliteWebResource(resource: unknown): SqliteWebResource {
    const { _handle, _client } = resource as {
        _handle: ConstructorParameters<typeof SqliteWebResource>[0];
        _client: ConstructorParameters<typeof SqliteWebResource>[1];
    };

    return new SqliteWebResource(_handle, _client);
}

const builder = await createBuilder();

await builder.subscribeAfterResourcesCreated(async (event) => {
    const model = await event.model.get();
    const sqliteWeb = asSqliteWebResource(await model.findResourceByName("sqlite-browser"));
    const _sqliteWebEndpoint = await sqliteWeb.primaryEndpoint.get();
    const _sqliteWebHost = await sqliteWeb.host.get();
    const _sqliteWebPort = await sqliteWeb.port.get();
    const _sqliteWebConnectionString = await sqliteWeb.connectionStringExpression.get();
    const _sqliteWebUri = await sqliteWeb.uriExpression.get();
});

const sqlite = await builder.addSqlite("sqlite", {
    databasePath: ".data/sqlite",
    databaseFileName: "app.db"
});
const sqliteDefault = await builder.addSqlite("sqlite-default");

await sqlite.withSqliteWeb({ containerName: "sqlite-browser" });
await sqliteDefault.withSqliteWeb();

const sqliteResource = sqlite;
const _sqliteConnectionString = await sqliteResource.connectionStringExpression.get();

await builder.build().run();

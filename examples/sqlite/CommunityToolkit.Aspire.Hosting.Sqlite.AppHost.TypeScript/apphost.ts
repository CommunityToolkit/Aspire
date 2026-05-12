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
    const model = await event.model();
    const sqliteWeb = asSqliteWebResource(await model.findResourceByName("sqlite-browser"));
    const _sqliteWebEndpoint = await sqliteWeb.getEndpoint("http");
    const _sqliteWebHost = await _sqliteWebEndpoint.host();
    const _sqliteWebPort = await _sqliteWebEndpoint.port();
    const _sqliteWebConnectionString = await sqliteWeb.connectionStringExpression();
    const _sqliteWebUri = await _sqliteWebEndpoint.url();
});

const sqlite = await builder.addSqlite("sqlite", {
    databasePath: ".data/sqlite",
    databaseFileName: "app.db"
});
const sqliteDefault = await builder.addSqlite("sqlite-default");

await sqlite.withSqliteWeb({ containerName: "sqlite-browser" });
await sqliteDefault.withSqliteWeb();

const sqliteResource = sqlite;
const _sqliteConnectionString = await sqliteResource.connectionStringExpression();

await builder.build().run();

import path from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const primaryConfigPath = path.join(appHostDirectory, "dab-config.json");
const secondaryConfigPath = path.join(appHostDirectory, "dab-config-2.json");

const sql = await builder
    .addSqlServer("sql")
    .withBindMount("./sql-server", "/usr/config")
    .withBindMount("../database", "/docker-entrypoint-initdb.d")
    .withEntrypoint("/usr/config/entrypoint.sh");

const db = await sql.addDatabase("trek");

const dab = await builder
    .addDataAPIBuilder("dab", {
        configFilePaths: [primaryConfigPath],
    })
    .waitFor(sql)
    .withReference(db);

const dabWithOptions = await builder
    .addDataAPIBuilder("dab-with-options", {
        configFilePaths: [primaryConfigPath, secondaryConfigPath],
        httpPort: 5001,
    })
    .waitFor(sql)
    .withReference(db);

const _primaryEndpoint = await dab.primaryEndpoint();
const _host = await dab.host();
const _port = await dab.port();
const _uri = await dab.uriExpression();

const _secondaryPrimaryEndpoint = await dabWithOptions.primaryEndpoint();
const _secondaryHost = await dabWithOptions.host();
const _secondaryPort = await dabWithOptions.port();
const _secondaryUri = await dabWithOptions.uriExpression();

await builder.build().run();

import path from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const projectPath = path.join(
    appHostDirectory,
    "..",
    "SdkProject",
    "SdkProject.csproj",
);
const publishProfilePath = path.join(
    appHostDirectory,
    "..",
    "SdkProject",
    "Database.publish.xml",
);

const sqlServer = await builder.addSqlServer("sql");
const database = await sqlServer.addDatabase("TargetDatabase");
const existingConnection = await builder.addConnectionString("Aspire");

const databaseProject = await builder.addSqlProjectFromPath("database-project", projectPath);
await databaseProject.withDacDeployOptions(publishProfilePath);
await databaseProject.withSkipWhenDeployed();
await databaseProject.withReference(database);

const connectionProject = await builder.addSqlProjectFromPath("connection-project", projectPath);
await connectionProject.withConnectionReference(existingConnection);
await connectionProject.withExplicitStart();

const packageDatabase = await sqlServer.addDatabase("PackageDatabase");
const packageProject = await builder.addSqlPackage("package-project", "ErikEJ.Dacpac.Chinook", "1.0.0");
await packageProject.withReference(packageDatabase);

const _databaseProjectDacpacPath: string | null =
    await databaseProject.dacpacPath();
const _databaseProjectDacDeployOptionsPath: string | null =
    await databaseProject.dacDeployOptionsPath();
const _databaseProjectSkipWhenDeployed: boolean =
    await databaseProject.skipWhenDeployed();
const _connectionProjectDacpacPath: string | null =
    await connectionProject.dacpacPath();

await builder.build().run();


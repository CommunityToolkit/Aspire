import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const dacpacPath = path.join(appHostDirectory, 'validation.dacpac');
const publishProfilePath = path.join(appHostDirectory, 'Database.publish.xml');

const sqlServer = await builder.addSqlServer('sql');
const database = await sqlServer.addDatabase('TargetDatabase');
const existingConnection = await builder.addConnectionString('Aspire');

const databaseProject = await builder.addSqlProject('database-project');
await databaseProject.withDacpac(dacpacPath);
await databaseProject.withDacDeployOptions(publishProfilePath);
await databaseProject.withSkipWhenDeployed();
await databaseProject.withReference(database);

const connectionProject = await builder.addSqlProject('connection-project');
await connectionProject.withDacpac(dacpacPath);
await connectionProject.withConnectionReference(existingConnection);

const _databaseProjectDacpacPath: string | null = await databaseProject.dacpacPath.get();
const _databaseProjectDacDeployOptionsPath: string | null = await databaseProject.dacDeployOptionsPath.get();
const _databaseProjectSkipWhenDeployed: boolean = await databaseProject.skipWhenDeployed.get();
const _connectionProjectDacpacPath: string | null = await connectionProject.dacpacPath.get();

await builder.build().run();

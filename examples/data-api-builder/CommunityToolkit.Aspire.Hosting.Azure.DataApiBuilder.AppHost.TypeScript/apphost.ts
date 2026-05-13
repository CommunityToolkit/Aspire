import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const primaryConfigPath = path.join(appHostDirectory, 'dab-config.json');
const secondaryConfigPath = path.join(appHostDirectory, 'dab-config-2.json');

const dab = await builder._addDataAPIBuilderInternal('dab', [primaryConfigPath]);
const dabWithOptions = await builder._addDataAPIBuilderInternal('dab-with-options', [primaryConfigPath, secondaryConfigPath], 5001);

const _primaryEndpoint = await dab.primaryEndpoint.get();
const _host = await dab.host.get();
const _port = await dab.port.get();
const _uri = await dab.uriExpression.get();

const _secondaryPrimaryEndpoint = await dabWithOptions.primaryEndpoint.get();
const _secondaryHost = await dabWithOptions.host.get();
const _secondaryPort = await dabWithOptions.port.get();
const _secondaryUri = await dabWithOptions.uriExpression.get();

await builder.build().run();
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));

const dab = await builder.addDataAPIBuilder('dab');
const dabWithOptions = await builder.addDataAPIBuilder('dab-with-options', {
    configFilePaths: [
        path.join(appHostDirectory, 'dab-config.json'),
        path.join(appHostDirectory, 'dab-config-2.json')
    ],
    httpPort: 5001
});

const _primaryEndpoint = await dab.primaryEndpoint.get();
const _host = await dab.host.get();
const _port = await dab.port.get();
const _uri = await dab.uriExpression.get();

const _secondaryPrimaryEndpoint = await dabWithOptions.primaryEndpoint.get();
const _secondaryHost = await dabWithOptions.host.get();
const _secondaryPort = await dabWithOptions.port.get();
const _secondaryUri = await dabWithOptions.uriExpression.get();

await builder.build().run();
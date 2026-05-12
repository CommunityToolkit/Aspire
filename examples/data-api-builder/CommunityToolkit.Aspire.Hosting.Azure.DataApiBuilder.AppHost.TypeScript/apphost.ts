import path from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const primaryConfigPath = path.join(appHostDirectory, "dab-config.json");
const secondaryConfigPath = path.join(appHostDirectory, "dab-config-2.json");

const dab = await builder.addDataAPIBuilder("dab", {
    configFilePaths: [primaryConfigPath],
});
const dabWithOptions = await builder.addDataAPIBuilder("dab-with-options", {
    configFilePaths: [primaryConfigPath, secondaryConfigPath],
    httpPort: 5001,
});

const _primaryEndpoint = await dab.getEndpoint("http");
const _host = await _primaryEndpoint.host();
const _port = await _primaryEndpoint.port();
const _uri = await _primaryEndpoint.url();

const _secondaryPrimaryEndpoint = await dabWithOptions.getEndpoint("http");
const _secondaryHost = await _secondaryPrimaryEndpoint.host();
const _secondaryPort = await _secondaryPrimaryEndpoint.port();
const _secondaryUri = await _secondaryPrimaryEndpoint.url();

await builder.build().run();

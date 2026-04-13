import { mkdir, chmod } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();
const appHostDirectory = dirname(fileURLToPath(import.meta.url));
const dataDirectory = join(appHostDirectory, "solr-data");

await mkdir(dataDirectory, { recursive: true, mode: 0o777 });

// force 777 permissions for the data directory to ensure Solr can write to it as the Docker container needs to have
// write permissions on the folder, or you have to change the folder owner to the UID in the container
await chmod(dataDirectory, 0o777);

const solr = await builder.addSolr("solr");
const builderCoreName: string = await solr.coreName.get();
await solr.coreName.set(builderCoreName);
await solr.withDataVolume({ name: "solr-data" });

const resolvedSolr = await solr;
const _primaryEndpoint = await resolvedSolr.primaryEndpoint.get();
const _host = await resolvedSolr.host.get();
const _port = await resolvedSolr.port.get();
const _coreName: string = await resolvedSolr.coreName.get();
const _connectionString = await resolvedSolr.connectionStringExpression.get();
const _uriExpression = await resolvedSolr.uriExpression.get();

const bindMountedSolr = await builder.addSolr("solr-bind", {
    port: 8984,
    coreName: "bindcore",
});
const bindCoreName: string = await bindMountedSolr.coreName.get();
await bindMountedSolr.coreName.set(bindCoreName);
await bindMountedSolr.withDataBindMount(dataDirectory);

if (false) {
    const configsetSolr = await builder.addSolr("solr-configset", {
        coreName: "configset-core",
    });
    await configsetSolr.withConfigset(
        "sample-configset",
        "./configsets/sample-configset",
    );
}

await builder.build().run();

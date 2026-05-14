import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

let primary = await builder.addSurrealServer("primary", {
    port: 18000,
    path: "memory",
    strictMode: true,
});

primary = await primary.withDataVolume({ name: "surreal-primary-data" });
primary = await primary.withLogLevel("Information");
primary = await primary.withSurrealist({ containerName: "surrealist-primary" });
primary = await primary.withSurrealDbOtlpExporter();

let appNamespace = await primary.addNamespace("appns", {
    namespaceName: "polyglotNs",
});
appNamespace = await appNamespace.withCreationScript(
    "DEFINE NAMESPACE IF NOT EXISTS `polyglotNs`;",
);

let appDatabase = await appNamespace.addDatabase("appdb", {
    databaseName: "polyglotDb",
});
appDatabase = await appDatabase.withCreationScript(
    "DEFINE DATABASE IF NOT EXISTS `polyglotDb`;",
);

let mounted = await builder.addSurrealServer("mounted", {
    port: 18001,
    path: "memory",
});
mounted = await mounted.withDataBindMount("./data");
mounted = await mounted.withInitFiles("./seed.surql");

const _primaryEndpoint = await primary.primaryEndpoint();
const _primaryHost = await primary.host();
const _primaryPort = await primary.port();
const _primaryPasswordParameter = await primary.passwordParameter();
const _primaryUri = await primary.uriExpression();
const _primaryConnectionString = await primary.connectionStringExpression();

const _namespaceParent = await appNamespace.parent();
const _namespaceConnectionString =
    await appNamespace.connectionStringExpression();
const _namespaceName = await appNamespace.namespaceName();
const _namespaceParentName = await _namespaceParent.name();

const _databaseParent = await appDatabase.parent();
const _databaseConnectionString =
    await appDatabase.connectionStringExpression();
const _databaseName = await appDatabase.databaseName();
const _databaseParentName = await _databaseParent.name();
const _databaseServerName = await (await _databaseParent.parent()).name();

const _mountedEndpoint = await mounted.primaryEndpoint();
const _mountedHost = await mounted.host();
const _mountedPort = await mounted.port();
const _mountedUri = await mounted.uriExpression();
const _mountedConnectionString = await mounted.connectionStringExpression();

await builder.build().run();

import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

const rootUser = await builder.addParameter("minio-root-user", {
    value: "minioadmin",
});
const rootPassword = await builder.addParameter("minio-root-password", {
    value: "minio-password",
    secret: true,
});
const overrideUser = await builder.addParameter("override-root-user", {
    value: "override-admin",
});
const overridePassword = await builder.addParameter("override-root-password", {
    value: "override-password",
    secret: true,
});

const minio = await builder.addMinioContainer("minio", {
    rootUser,
    rootPassword,
    port: 9000,
});

const minioDefaults = await builder.addMinioContainer("minio-defaults");
const minioBindMount = await builder.addMinioContainer("minio-bind-mount", {
    rootUser: overrideUser,
    rootPassword: overridePassword,
});

await minio.withHostPort({ port: 9100 }).withDataVolume({ name: "minio-data" });
await minioDefaults.withUserName(overrideUser).withPassword(overridePassword);
await minioDefaults.withDataVolume();
await minioBindMount.withDataBindMount("./runtime-data/minio-bind");

const _minioRootUser = await minio.rootUser.get();
const _minioPasswordParameter = await minio.passwordParameter.get();
const _minioPrimaryEndpoint = await minio.primaryEndpoint();
const _minioHost = await minio.host();
const _minioPort = await minio.port();
const _minioUri = await minio.uriExpression();
const _minioConnectionString = await minio.connectionStringExpression();

const _defaultsPrimaryEndpoint = await minioDefaults.primaryEndpoint();
const _defaultsHost = await minioDefaults.host();
const _defaultsPort = await minioDefaults.port();
const _defaultsUri = await minioDefaults.uriExpression();
const _defaultsConnectionString =
    await minioDefaults.connectionStringExpression();

const _bindMountPrimaryEndpoint = await minioBindMount.primaryEndpoint();
const _bindMountHost = await minioBindMount.host();
const _bindMountPort = await minioBindMount.port();
const _bindMountUri = await minioBindMount.uriExpression();
const _bindMountConnectionString =
    await minioBindMount.connectionStringExpression();

await builder.build().run();

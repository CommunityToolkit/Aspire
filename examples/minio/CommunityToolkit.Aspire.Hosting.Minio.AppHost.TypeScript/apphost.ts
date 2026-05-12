import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

const rootUser = await builder.addParameterWithValue(
    "minio-root-user",
    "minioadmin",
);
const rootPassword = await builder.addParameterWithValue(
    "minio-root-password",
    "minio-password",
    { secret: true },
);
const overrideUser = await builder.addParameterWithValue(
    "override-root-user",
    "override-admin",
);
const overridePassword = await builder.addParameterWithValue(
    "override-root-password",
    "override-password",
    { secret: true },
);

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

const _minioRootUser = await minio.rootUser();
const _minioPasswordParameter = await minio.passwordParameter();
const _minioPrimaryEndpoint = await minio.getEndpoint("http");
const _minioHost = await _minioPrimaryEndpoint.host();
const _minioPort = await _minioPrimaryEndpoint.port();
const _minioUri = await _minioPrimaryEndpoint.url();
const _minioConnectionString = await minio.connectionStringExpression();

const _defaultsPrimaryEndpoint = await minioDefaults.getEndpoint("http");
const _defaultsHost = await _defaultsPrimaryEndpoint.host();
const _defaultsPort = await _defaultsPrimaryEndpoint.port();
const _defaultsUri = await _defaultsPrimaryEndpoint.url();
const _defaultsConnectionString =
    await minioDefaults.connectionStringExpression();

const _bindMountPrimaryEndpoint = await minioBindMount.getEndpoint("http");
const _bindMountHost = await _bindMountPrimaryEndpoint.host();
const _bindMountPort = await _bindMountPrimaryEndpoint.port();
const _bindMountUri = await _bindMountPrimaryEndpoint.url();
const _bindMountConnectionString =
    await minioBindMount.connectionStringExpression();

await builder.build().run();

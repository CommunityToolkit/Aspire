import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const rootUser = await builder.addParameterWithValue("minio-root-user", "minioadmin");
const rootPassword = await builder.addParameterWithValue("minio-root-password", "minio-password", { secret: true });
const overrideUser = await builder.addParameterWithValue("override-root-user", "override-admin");
const overridePassword = await builder.addParameterWithValue("override-root-password", "override-password", { secret: true });

const minio = await builder.addMinioContainer("minio", {
    rootUser,
    rootPassword,
    port: 9000
});

const minioDefaults = await builder.addMinioContainer("minio-defaults");
const minioBindMount = await builder.addMinioContainer("minio-bind-mount", {
    rootUser: overrideUser,
    rootPassword: overridePassword
});

await minio.withHostPort({ port: 9100 }).withDataVolume({ name: "minio-data" });
await minioDefaults.withUserName(overrideUser).withPassword(overridePassword);
await minioDefaults.withDataVolume();
await minioBindMount.withDataBindMount("./runtime-data/minio-bind");

const minioResource = await minio;
const _minioRootUser = await minioResource.rootUser.get();
const _minioPasswordParameter = await minioResource.passwordParameter.get();
const _minioPrimaryEndpoint = await minioResource.primaryEndpoint.get();
const _minioHost = await minioResource.host.get();
const _minioPort = await minioResource.port.get();
const _minioUri = await minioResource.uriExpression.get();
const _minioConnectionString = await minioResource.connectionStringExpression.get();

const minioDefaultsResource = await minioDefaults;
const _defaultsPrimaryEndpoint = await minioDefaultsResource.primaryEndpoint.get();
const _defaultsHost = await minioDefaultsResource.host.get();
const _defaultsPort = await minioDefaultsResource.port.get();
const _defaultsUri = await minioDefaultsResource.uriExpression.get();
const _defaultsConnectionString = await minioDefaultsResource.connectionStringExpression.get();

const minioBindMountResource = await minioBindMount;
const _bindMountPrimaryEndpoint = await minioBindMountResource.primaryEndpoint.get();
const _bindMountHost = await minioBindMountResource.host.get();
const _bindMountPort = await minioBindMountResource.port.get();
const _bindMountUri = await minioBindMountResource.uriExpression.get();
const _bindMountConnectionString = await minioBindMountResource.connectionStringExpression.get();

await builder.build().run();

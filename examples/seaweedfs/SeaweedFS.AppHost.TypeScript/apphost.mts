import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

// 1. S3-Compatible API Gateway
const seaweedS3 = await builder.addSeaweedFS("seaweedfs-s3");
await seaweedS3.withS3();
await seaweedS3.withDataVolume();

// 2. Native Filer API Only
const seaweedFiler = await builder.addSeaweedFS("seaweedfs-filer");
await seaweedFiler.withFiler();

// Get connection strings to ensure the expressions are evaluated properly by ATS
const _s3ConnectionString = await seaweedS3.connectionStringExpression();
const _filerConnectionString = await seaweedFiler.connectionStringExpression();

await builder.build().run();
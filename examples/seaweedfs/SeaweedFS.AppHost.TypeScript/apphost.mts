import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

// 1. S3-Compatible API Gateway
const seaweedS3 = await builder.addSeaweedFS("typescript-seaweedfs-s3");
await seaweedS3.withS3();
await seaweedS3.withDataVolume();

// 2. Native Filer API Only
const seaweedFiler = await builder.addSeaweedFS("typescript-seaweedfs-filer");
await seaweedFiler.withFiler();

await builder.build().run();
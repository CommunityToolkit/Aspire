import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const accessKey = await builder.addParameter("accessKey", {
    value: "rustfsadmin",
});
const secretKey = await builder.addParameter("secretKey", {
    value: "rustfsadmin",
    secret: true,
});

const rustFs = await builder.addRustFs("rustfs", {
    accessKey,
    secretKey,
});
await rustFs.withDataVolume();
await rustFs.addBucket("mybucket");

await builder.build().run();

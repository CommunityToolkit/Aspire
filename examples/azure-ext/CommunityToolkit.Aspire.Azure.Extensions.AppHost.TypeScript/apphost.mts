import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const storage = await builder.addAzureStorage("azure-storage").runAsEmulator({
    configureContainer: async (azurite) => {
        await azurite
            .withArgs(["--disableProductStyleUrl"])
            .withBlobPort(27000)
            .withQueuePort(27001)
            .withTablePort(27002)
            .withDataVolume({ name: "storage" });
    },
});

await storage.addBlobs("blobs").withAzureStorageExplorer();

await builder.build().run();

import path from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder, RavenDBCloudDiskLayout, RavenDBCloudProvider, RavenDBCloudStorageType } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiServiceProjectPath = path.join(
    appHostDirectory,
    "..",
    "CommunityToolkit.Aspire.Hosting.RavenDB.ApiService",
    "CommunityToolkit.Aspire.Hosting.RavenDB.ApiService.csproj",
);

// The applications run in Docker Compose; RavenDB Cloud gets them the product URL and a certificate of their own.
await builder.addDockerComposeEnvironment("compose");

const apiKey = await builder.addParameter("ravendb-cloud-api-key", { secret: true });

// aspire run starts a local RavenDB container; aspire deploy finds or creates the RavenDB Cloud product.
const ravendb = await builder.addRavenDB("ravendb")
    .publishAsRavenDBCloud(
        apiKey,
        "aspire-ravendb-example",
        async spec => {
            await spec.development(
                RavenDBCloudProvider.Aws,
                "eu-central-1", // region
                "Dev10", // instance type
                10, // disk size in GB
                RavenDBCloudStorageType.SsdStandard,
                RavenDBCloudDiskLayout.SingleDataDisk,
                "Stable72"); // release channel
        },
        { configure: async cloud => { await cloud.withAllowedIps(["203.0.113.0/24"]); } });

await ravendb.addDatabase("ravenDatabase", { ensureCreated: true });

const apiService = await builder.addDotnetProject("apiservice", apiServiceProjectPath);
await apiService.withReference(ravendb);
await apiService.waitFor(ravendb);

await builder.build().run();

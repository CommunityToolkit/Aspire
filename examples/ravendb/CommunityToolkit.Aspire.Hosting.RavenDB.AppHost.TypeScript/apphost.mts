import path from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();
const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiServiceProjectPath = path.join(
    appHostDirectory,
    "..",
    "CommunityToolkit.Aspire.Hosting.RavenDB.ApiService",
    "CommunityToolkit.Aspire.Hosting.RavenDB.ApiService.csproj",
);

const ravendb = await builder.addRavenDB("ravendb");
await ravendb.addDatabase("ravenDatabase");

const apiService = await builder.addProject("apiservice", apiServiceProjectPath);
await apiService.withReference(ravendb);
await apiService.waitFor(ravendb);

await builder.build().run();

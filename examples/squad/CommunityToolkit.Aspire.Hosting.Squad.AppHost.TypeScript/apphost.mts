import { createBuilder } from "./.aspire/modules/aspire.mjs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const builder = await createBuilder();

// Two Squad teams, each with its own .squad/ folder next to the C# AppHost.
const csharpAppHostDir = path.resolve(__dirname, "..", "CommunityToolkit.Aspire.Hosting.Squad.AppHost");
const researchSquadRoot = path.join(csharpAppHostDir, "research-squad");
const devSquadRoot = path.join(csharpAppHostDir, "dev-squad");

// addSquad — research squad (cast from The Matrix)
const researchSquad = await builder.addSquad("research-squad", {
    teamRoot: researchSquadRoot,
});

// addSquad — dev squad (cast from The Simpsons)
const devSquad = await builder.addSquad("dev-squad", {
    teamRoot: devSquadRoot,
});

await builder.build().run();

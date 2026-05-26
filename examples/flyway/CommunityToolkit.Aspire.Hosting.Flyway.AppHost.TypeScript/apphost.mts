import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

const flyway = await builder.addFlyway("flyway", "./migrations");
const flywayTelemetry = await builder.addFlyway(
    "flyway-telemetry",
    "./migrations",
);

await flyway.withArgs(["-v"]);
await flywayTelemetry.withTelemetryOptIn().withArgs(["-v"]);

await builder.build().run();


import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addDenoTask — exercise custom task options and fluent chaining.
await builder
    .addDenoTask("task-with-install", {
        workingDirectory: "./apps/deno-validation",
        taskName: "start",
        args: ["--", "--mode", "task-with-install"]
    })
    .withDenoPackageInstallation();

// addDenoTask — validate the default taskName overload.
await builder.addDenoTask("task-default", {
    workingDirectory: "./apps/deno-validation"
});

// addDenoApp — exercise permission flag and args arrays plus fluent chaining.
await builder
    .addDenoApp("app-with-install", "main.ts", {
        workingDirectory: "./apps/deno-validation",
        permissionFlags: ["--allow-env"],
        args: ["--mode", "app-with-install"]
    })
    .withDenoPackageInstallation();

// addDenoApp — validate the minimal options shape.
await builder.addDenoApp("app-minimal", "main.ts", {
    workingDirectory: "./apps/deno-validation"
});

await builder.build().run();

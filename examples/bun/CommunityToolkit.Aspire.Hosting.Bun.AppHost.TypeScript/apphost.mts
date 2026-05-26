import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addBunApp — explicit working directory and entry point
const bunApp = await builder.addBunApp("bun-app", {
    workingDirectory: "./bun-app",
    entryPoint: "index.ts",
    watch: false
});

// withBunPackageInstallation — default invocation
await bunApp.withBunPackageInstallation();
await bunApp.withBunPackageInstallation();

// addBunApp — exercise default entry point and watch values
const bunDefaults = await builder.addBunApp("bun-defaults", {
    workingDirectory: "./bun-app"
});
await bunDefaults.withBunPackageInstallation();

// addBunApp — exercise watch mode
const bunWatch = await builder.addBunApp("bun-watch", {
    workingDirectory: "./bun-app",
    entryPoint: "index.ts",
    watch: true
});
await bunWatch.withBunPackageInstallation();

await builder.build().run();

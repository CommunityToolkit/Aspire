import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

builder
    .addPowerShell("pwsh", {
        languageMode: "ConstrainedLanguage",
        minRunspaces: 1,
        maxRunspaces: 2,
    })
    .addScript("echo-message", "param([string]$message) Write-Output $message")
    .withArgs(["-message", "Hello from Aspire TypeScript"]);

await builder.build().run();


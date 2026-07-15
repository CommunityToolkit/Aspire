import { createBuilder } from "./.aspire/modules/aspire.mjs";

const builder = await createBuilder();

// TS bindings in this apphost expose connection strings, but not AddAzureStorage/AddBlobs.
const blob = await builder.addConnectionString("myblob");

// TS PowerShell runspace currently exposes waitFor, but not withReference.
const powershell = await builder.addPowerShell("ps").waitFor(blob);

const script1 = await powershell
    .addScript(
        "script1",
        `
param($name)
write-information "Hello, $name"
write-information "\`$myblob is $myblob"
`,
    )
    .withReference(blob)
    .withArgs(["world"]);

await powershell
    .addScript(
        "script2",
        `
write-information "Getting there..."
write-information "the sum of 2 and 3 is $([int]$args[0] + [int]$args[1])"
`,
    )
    .withArgs(["2", "3"])
    .waitForCompletion(script1);

await builder.build().run();

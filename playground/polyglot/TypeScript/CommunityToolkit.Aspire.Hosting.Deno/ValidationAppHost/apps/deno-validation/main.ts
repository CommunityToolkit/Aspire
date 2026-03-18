const modeIndex = Deno.args.indexOf("--mode");
const mode = modeIndex >= 0 ? Deno.args[modeIndex + 1] ?? "default" : "default";

console.log(`Deno validation app running in ${mode} mode.`);

await new Promise(() => {});

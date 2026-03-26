import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const golangRoot = await builder.addGolangApp("golang-root", "./go-app", ".", {
    args: ["--mode", "root"],
    buildTags: ["validation"]
});
await golangRoot.withGoModTidy();

const golangCmd = await builder.addGolangApp("golang-cmd", "./go-app", "./cmd/server", {
    args: ["--mode", "cmd-server"]
});
await golangCmd.withGoModDownload({ install: false });

await builder.build().run();

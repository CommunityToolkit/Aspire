import { createBuilder } from "./.modules/aspire.js";

const builder = await createBuilder();

// addPapercutSmtp — configured ports
const papercut = await builder.addPapercutSmtp("papercut", {
    httpPort: 8080,
    smtpPort: 2525,
});

// addPapercutSmtp — minimal overload (defaults)
const papercutDefault = await builder.addPapercutSmtp("papercut-default");

// ---- Endpoint access on PapercutSmtpContainerResource ----
const _papercutHost = await papercut.host();
const _papercutPort = await papercut.port();
const _papercutUri = await papercut.uriExpression();
const _papercutConnectionString = await papercut.connectionStringExpression();

const _papercutDefaultHost = await papercutDefault.host();
const _papercutDefaultPort = await papercutDefault.port();
const _papercutDefaultUri = await papercutDefault.uriExpression();
const _papercutDefaultConnectionString =
    await papercutDefault.connectionStringExpression();

await builder.build().run();

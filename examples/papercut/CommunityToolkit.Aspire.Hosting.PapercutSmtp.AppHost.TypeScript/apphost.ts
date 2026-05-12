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
const _papercutEndpoint = await papercut.getEndpoint("smtp");
const _papercutHost = await _papercutEndpoint.host();
const _papercutPort = await _papercutEndpoint.port();
const _papercutUri = await _papercutEndpoint.url();
const _papercutConnectionString = await papercut.connectionStringExpression();

const _papercutDefaultEndpoint = await papercutDefault.getEndpoint("smtp");
const _papercutDefaultHost = await _papercutDefaultEndpoint.host();
const _papercutDefaultPort = await _papercutDefaultEndpoint.port();
const _papercutDefaultUri = await _papercutDefaultEndpoint.url();
const _papercutDefaultConnectionString =
    await papercutDefault.connectionStringExpression();

await builder.build().run();

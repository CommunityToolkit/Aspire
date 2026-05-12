import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addPapercutSmtp — configured ports
const papercut = await builder.addPapercutSmtp("papercut", {
    httpPort: 8080,
    smtpPort: 2525
});

// addPapercutSmtp — minimal overload (defaults)
const papercutDefault = await builder.addPapercutSmtp("papercut-default");

// ---- Endpoint access on PapercutSmtpContainerResource ----
const papercutResource = papercut;
const _papercutEndpoint = await papercutResource.getEndpoint("smtp");
const _papercutHost = await _papercutEndpoint.host.get();
const _papercutPort = await _papercutEndpoint.port.get();
const _papercutUri = await _papercutEndpoint.url.get();
const _papercutConnectionString = await papercutResource.connectionStringExpression.get();

const papercutDefaultResource = papercutDefault;
const _papercutDefaultEndpoint = await papercutDefaultResource.getEndpoint("smtp");
const _papercutDefaultHost = await _papercutDefaultEndpoint.host.get();
const _papercutDefaultPort = await _papercutDefaultEndpoint.port.get();
const _papercutDefaultUri = await _papercutDefaultEndpoint.url.get();
const _papercutDefaultConnectionString = await papercutDefaultResource.connectionStringExpression.get();

await builder.build().run();

import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

// addPapercutSmtp — configured ports
const papercut = await builder.addPapercutSmtp("papercut", {
    httpPort: 8080,
    smtpPort: 2525
});

// addPapercutSmtp — minimal overload (defaults)
const papercutDefault = await builder.addPapercutSmtp("papercut-default");

// ---- Property access on PapercutSmtpContainerResource (ExposeProperties = true) ----
const papercutResource = await papercut;
const _papercutHost = await papercutResource.host.get();
const _papercutPort = await papercutResource.port.get();
const _papercutUri = await papercutResource.uriExpression.get();
const _papercutConnectionString = await papercutResource.connectionStringExpression.get();

const papercutDefaultResource = await papercutDefault;
const _papercutDefaultHost = await papercutDefaultResource.host.get();
const _papercutDefaultPort = await papercutDefaultResource.port.get();
const _papercutDefaultUri = await papercutDefaultResource.uriExpression.get();
const _papercutDefaultConnectionString = await papercutDefaultResource.connectionStringExpression.get();

await builder.build().run();

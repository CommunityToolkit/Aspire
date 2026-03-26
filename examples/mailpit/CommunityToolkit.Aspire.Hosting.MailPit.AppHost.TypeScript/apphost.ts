import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { createBuilder } from './.modules/aspire.js';

const bindMountPath = mkdtempSync(join(tmpdir(), 'mailpit-'));

const builder = await createBuilder();

// addMailPit — explicit HTTP and SMTP port configuration
const mailpit = await builder.addMailPit("mailpit", {
    httpPort: 18025,
    smtpPort: 11025
});

// addMailPit — default ports
const mailpitDefault = await builder.addMailPit("mailpit-default");

// withDataVolume — add a named volume for MailPit persistence
await mailpit.withDataVolume("mailpit-data");

// withDataBindMount — bind mount a temporary host directory
await mailpitDefault.withDataBindMount(bindMountPath);

// ---- Property access on MailPitContainerResource (ExposeProperties = true) ----
const mailpitResource = await mailpit;
const _mailpitEndpoint = await mailpitResource.primaryEndpoint.get();
const _mailpitHost = await mailpitResource.host.get();
const _mailpitPort = await mailpitResource.port.get();
const _mailpitUri = await mailpitResource.uriExpression.get();
const _mailpitConnectionString = await mailpitResource.connectionStringExpression.get();

const mailpitDefaultResource = await mailpitDefault;
const _mailpitDefaultEndpoint = await mailpitDefaultResource.primaryEndpoint.get();
const _mailpitDefaultHost = await mailpitDefaultResource.host.get();
const _mailpitDefaultPort = await mailpitDefaultResource.port.get();
const _mailpitDefaultUri = await mailpitDefaultResource.uriExpression.get();
const _mailpitDefaultConnectionString = await mailpitDefaultResource.connectionStringExpression.get();

await builder.build().run();

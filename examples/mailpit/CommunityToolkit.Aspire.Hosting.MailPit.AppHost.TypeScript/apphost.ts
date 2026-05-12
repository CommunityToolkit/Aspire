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

// ---- Endpoint access on MailPitContainerResource ----
const mailpitResource = await mailpit;
const _mailpitEndpoint = await mailpitResource.getEndpoint("smtp");
const _mailpitHost = await _mailpitEndpoint.host.get();
const _mailpitPort = await _mailpitEndpoint.port.get();
const _mailpitUri = await _mailpitEndpoint.url.get();
const _mailpitConnectionString = await mailpitResource.connectionStringExpression.get();

const mailpitDefaultResource = await mailpitDefault;
const _mailpitDefaultEndpoint = await mailpitDefaultResource.getEndpoint("smtp");
const _mailpitDefaultHost = await _mailpitDefaultEndpoint.host.get();
const _mailpitDefaultPort = await _mailpitDefaultEndpoint.port.get();
const _mailpitDefaultUri = await _mailpitDefaultEndpoint.url.get();
const _mailpitDefaultConnectionString = await mailpitDefaultResource.connectionStringExpression.get();

await builder.build().run();

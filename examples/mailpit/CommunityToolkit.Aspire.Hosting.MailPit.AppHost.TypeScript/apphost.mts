import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

import { createBuilder } from "./.modules/aspire.js";

const bindMountPath = mkdtempSync(join(tmpdir(), "mailpit-"));

const builder = await createBuilder();

// addMailPit — explicit HTTP and SMTP port configuration
const mailpit = await builder.addMailPit("mailpit", {
    httpPort: 18025,
    smtpPort: 11025,
});

// addMailPit — default ports
const mailpitDefault = await builder.addMailPit("mailpit-default");

// withDataVolume — add a named volume for MailPit persistence
await mailpit.withDataVolume("mailpit-data");

// withDataBindMount — bind mount a temporary host directory
await mailpitDefault.withDataBindMount(bindMountPath);

// ---- Endpoint access on MailPitContainerResource ----
const _mailpitEndpoint = await mailpit.primaryEndpoint();
const _mailpitHost = await mailpit.host();
const _mailpitPort = await mailpit.port();
const _mailpitUri = await mailpit.uriExpression();
const _mailpitConnectionString = await mailpit.connectionStringExpression();

const _mailpitDefaultEndpoint = await mailpitDefault.primaryEndpoint();
const _mailpitDefaultHost = await mailpitDefault.host();
const _mailpitDefaultPort = await mailpitDefault.port();
const _mailpitDefaultUri = await mailpitDefault.uriExpression();
const _mailpitDefaultConnectionString =
    await mailpitDefault.connectionStringExpression();

await builder.build().run();

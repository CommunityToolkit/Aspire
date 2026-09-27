import { createBuilder, CrdWaitBehavior } from './.aspire/modules/aspire.mjs';
import { fileURLToPath } from 'node:url';

const builder = await createBuilder();
const cluster = await builder.addKindCluster('ts-kind');

let manifestConfigured = false;
const manifest = await cluster.addManifestFromContent('ts-manifest', `
apiVersion: v1
kind: Namespace
metadata:
  name: ts-crd-wait
`);
await manifest.withCrdWait(async options => {
    await options.timeout.set(120_000);
    await options.failureBehavior.set(CrdWaitBehavior.BestEffort);
    if (await options.timeout.get() !== 120_000 ||
        await options.failureBehavior.get() !== CrdWaitBehavior.BestEffort) {
        throw new Error('Manifest CRD-wait options did not round-trip through the generated SDK.');
    }
    manifestConfigured = true;
});

let chartConfigured = false;
const chart = await cluster.addHelmChart('ts-chart', fileURLToPath(new URL('./chart', import.meta.url)));
await chart.withCrdWait(async options => {
    await options.timeout.set(180_000);
    await options.failureBehavior.set(CrdWaitBehavior.BestEffort);
    if (await options.timeout.get() !== 180_000 ||
        await options.failureBehavior.get() !== CrdWaitBehavior.BestEffort) {
        throw new Error('Helm CRD-wait options did not round-trip through the generated SDK.');
    }
    chartConfigured = true;
});

if (!manifestConfigured || !chartConfigured) {
    throw new Error('CRD-wait callbacks were not invoked for both Kind builders.');
}

if (await manifest.crdWaitTimeout.get() !== 120_000 ||
    await manifest.crdWaitBehavior.get() !== CrdWaitBehavior.BestEffort) {
    throw new Error('Manifest CRD-wait policy did not retain the configured options.');
}

await manifest.withCrdWaitTimeout(90_000);
await manifest.withCrdWaitBehavior(CrdWaitBehavior.Fail);
if (await manifest.crdWaitTimeout.get() !== 90_000 ||
    await manifest.crdWaitBehavior.get() !== CrdWaitBehavior.Fail) {
    throw new Error('Legacy manifest CRD-wait methods did not update the configured options.');
}

await builder.build().run();

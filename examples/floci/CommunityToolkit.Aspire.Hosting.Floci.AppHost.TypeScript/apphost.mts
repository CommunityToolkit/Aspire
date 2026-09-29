import { mkdirSync } from "node:fs";
import { randomUUID } from "node:crypto";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();

// ── Runtime path (actually executed) ─────────────────────────────────────────
const flociAws = await builder.addFlociAws('floci-aws');
const flociAzure = await builder.addFlociAzure('floci-az');
const flociGcp = await builder.addFlociGcp('floci-gcp');

await flociAzure.withDockerSocket();
await flociAzure.withEnvironment('FLOCI_AZ_DOCKER_RESOURCE_NAMESPACE', `sample-${randomUUID()}`);
const serviceBus = await flociAzure.withServiceBus({ name: 'servicebus' });
await serviceBus.amqpEndpoint();
await serviceBus.amqpTlsEndpoint();
await serviceBus.connectionStringExpression();

// A single Floci UI console browses all three clouds — flociAws.withFlociUI() creates the
// console wired to AWS, then withCloudReference* attaches the Azure and GCP resources to it.
await flociAws.withFlociUI({
    configureContainer: async (ui) => {
        await ui.withAzureReference(flociAzure);
        await ui.withGcpReference(flociGcp);
    },
});

const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiServiceProject = "CommunityToolkit.Aspire.Hosting.Floci.ApiService";
const apiServiceProjectPath = path.join(appHostDirectory, "..", apiServiceProject, apiServiceProject + ".csproj");

const apiService = await builder.addProject("floci-api", apiServiceProjectPath)
    .withExternalHttpEndpoints()
    .withHttpHealthCheck({ path: "/health" })
    .withFlociAwsReference(flociAws)
    .withFlociAzureReference(flociAzure)
    .withFlociGcpReference(flociGcp)
    .withFlociAzureServiceBusReference(serviceBus)
    .waitFor(flociAws)
    .waitFor(flociAzure)
    .waitFor(flociGcp);

// Resolve the returned child handle from a real container and require an AMQP response.
await builder.addContainer('servicebus-probe', 'node:22-alpine')
    .withFlociAzureServiceBusReference(serviceBus)
    .withHttpEndpoint({ targetPort: 8080 })
    .withHttpHealthCheck({ path: '/' })
    .waitFor(flociAzure)
    .withArgs(['-e', `
        const net = require('node:net');
        const http = require('node:http');
        const assert = require('node:assert/strict');
        const connectionString = process.env.ConnectionStrings__servicebus;
        const endpoint = new URL(connectionString.split(';')[0].slice('Endpoint='.length));
        const header = Buffer.from([65, 77, 81, 80, 0, 1, 0, 0]);
        let ready = false;
        const socket = net.connect({ host: endpoint.hostname, port: Number(endpoint.port) }, () => socket.write(header));
        socket.setTimeout(20000, () => { throw new Error('AMQP handshake timed out'); });
        let response = Buffer.alloc(0);
        socket.on('data', data => {
            response = Buffer.concat([response, data]);
            if (response.length >= header.length) {
                assert.deepEqual(response.subarray(0, header.length), header);
                ready = true;
                socket.destroy();
            }
        });
        http.createServer((request, response) => {
            response.writeHead(ready ? 200 : 503);
            response.end();
        }).listen(8080, '0.0.0.0');
    `]);

// ── Custom port and region ────────────────────────────────────────────────────
await builder.addFlociAws('floci-custom', {
    port: 14566,
    defaultRegion: 'eu-west-1',
    defaultAccountId: '123456789012',
});

// ── Custom project ID (GCP) ───────────────────────────────────────────────────
await builder.addFlociGcp('floci-gcp-custom', {
    defaultProjectId: 'my-project',
});

// ── Persistent storage — named volume ─────────────────────────────────────────
// Switches Floci from in-memory to persistent mode automatically.
const flociPersistent = await builder.addFlociAws('floci-persistent');
await flociPersistent.withDataVolume('floci-data');

// ── Persistent storage — bind mount ───────────────────────────────────────────
const flociMount = await builder.addFlociAws('floci-mount');
const flociDataPath = path.join(appHostDirectory, '.aspire', 'floci-data');
mkdirSync(flociDataPath, { recursive: true });
await flociMount.withDataBindMount(flociDataPath);

// ── Compile-time coverage ─────────────────────────────────────────────────────
// Guards with false so these are type-checked but never executed.
// Covers API surface that requires special host setup (Docker socket, cert files) or that
// would just be redundant with the AWS coverage already exercised on the runtime path above.
const includeCompileOnlyScenarios = false;

if (includeCompileOnlyScenarios) {

    // ── Floci UI web console — custom container name and host port ────────────
    const _withUi = await builder.addFlociAws('floci-with-ui');
    await _withUi.withFlociUI({
        containerName: 'my-floci-ui',
        configureContainer: async (ui) => {
            await ui.withHostPort({ port: 14500 });
        },
    });

    // ── Custom socket path ────────────────────────────────────────────────────
    // Non-standard Docker installations (Podman, Rancher Desktop) expose the
    // socket at a different path; pass it explicitly. Available on all three clouds.
    const _podman = await builder.addFlociAws('floci-podman');
    await _podman.withDockerSocket({
        socketPath: '/run/user/1000/podman/podman.sock'
    });

    const _azurePodman = await builder.addFlociAzure('floci-az-podman');
    await _azurePodman.withDockerSocket({
        socketPath: '/run/user/1000/podman/podman.sock'
    });
    await _azurePodman.withServiceBus({ name: 'podman-servicebus', amqpPort: 15673, amqpTlsPort: 15674 });

    const _gcpPodman = await builder.addFlociGcp('floci-gcp-podman');
    await _gcpPodman.withDockerSocket({
        socketPath: '/run/user/1000/podman/podman.sock'
    });

    // ── Persistent storage — Azure and GCP ─────────────────────────────────────
    const _azurePersistent = await builder.addFlociAzure('floci-az-persistent');
    await _azurePersistent.withDataVolume('floci-az-data');

    const _azureMount = await builder.addFlociAzure('floci-az-mount');
    await _azureMount.withDataBindMount('/tmp/floci-az-data');

    const _gcpPersistent = await builder.addFlociGcp('floci-gcp-persistent');
    await _gcpPersistent.withDataVolume('floci-gcp-data');

    const _gcpMount = await builder.addFlociGcp('floci-gcp-mount');
    await _gcpMount.withDataBindMount('/tmp/floci-gcp-data');

    // ── TLS / HTTPS (AWS and Azure) ────────────────────────────────────────────
    // Both images serve HTTP and HTTPS on the same port, so enabling TLS never changes
    // the port — only the scheme handed to dependents. No TLS on GCP: that image has
    // no HTTPS listener.
    //
    // Aspire provisions and mounts the key pair; the integration maps its paths onto the
    // image's own FLOCI_TLS_* settings and switches the endpoint to https.
    const _tlsDevCert = await builder.addFlociAws('floci-tls');
    await _tlsDevCert.withHttpsDeveloperCertificate();

    const _tlsAzureDevCert = await builder.addFlociAzure('floci-az-tls');
    await _tlsAzureDevCert.withHttpsDeveloperCertificate();

    // ── Custom Quarkus config file (AWS only) ──────────────────────────────────
    // Mounts application.yml read-only at /deployments/config/application.yml
    // inside the container so Quarkus merges it with built-in defaults on startup.
    const _configured = await builder.addFlociAws('floci-configured');
    await _configured.withConfigFile('./floci.yml');

    // ── Connection string / endpoint properties — all three clouds ────────────
    //   connectionStringExpression → an Aspire endpoint expression; resolved to localhost:{hostPort}
    //                               for host processes and {name}:{targetPort} for sibling containers.
    const _awsEndpoint = await flociAws.primaryEndpoint();
    const _awsHost = await flociAws.host();
    const _awsPort = await flociAws.port();
    const _awsConnectionString = await flociAws.connectionStringExpression();

    const _azureEndpoint = await flociAzure.primaryEndpoint();
    const _azureHost = await flociAzure.host();
    const _azurePort = await flociAzure.port();
    const _azureConnectionString = await flociAzure.connectionStringExpression();

    const _gcpEndpoint = await flociGcp.primaryEndpoint();
    const _gcpHost = await flociGcp.host();
    const _gcpPort = await flociGcp.port();
    const _gcpConnectionString = await flociGcp.connectionStringExpression();

    // ── WithReference — env var injection per cloud ────────────────────────────
    //   withFlociAwsReference / withFlociAzureReference / withFlociGcpReference inject:
    //     ConnectionStrings__floci        = http://localhost:{port}
    //     AWS_ENDPOINT_URL                = the emulator endpoint, resolved per dependent by Aspire
    //     AWS_DEFAULT_REGION              = us-east-1
    //     AWS_ACCESS_KEY_ID               = test
    //     AWS_SECRET_ACCESS_KEY           = test
    //   For Azure: AZURE_STORAGE_CONNECTION_STRING (devstoreaccount1 dev credentials)
    //   For GCP: PUBSUB_EMULATOR_HOST, FIRESTORE_EMULATOR_HOST, DATASTORE_EMULATOR_HOST,
    //            STORAGE_EMULATOR_HOST, SECRET_MANAGER_EMULATOR_HOST, GOOGLE_CLOUD_PROJECT
    const _project = await builder
        .addProject('api', '../FlociApi/FlociApi.csproj')
        .withFlociAwsReference(flociAws)
        .withFlociAzureReference(flociAzure)
        .withFlociGcpReference(flociGcp);

    const _container = await builder
        .addContainer('worker', 'myorg/worker')
        .withFlociAwsReference(flociAws)
        .withFlociAzureReference(flociAzure)
        .withFlociGcpReference(flociGcp);
}

await builder.build().run();

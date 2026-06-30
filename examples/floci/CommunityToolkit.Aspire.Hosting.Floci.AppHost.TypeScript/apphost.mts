import path from "node:path";
import { fileURLToPath } from "node:url";
import { createBuilder } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();

// ── Runtime path (actually executed) ─────────────────────────────────────────
// Single Floci instance with the Docker socket mounted so Lambda and other
// container-backed AWS services can launch sibling containers.
const floci = await builder.addFloci('floci');

const appHostDirectory = path.dirname(fileURLToPath(import.meta.url));
const apiServiceProject = "CommunityToolkit.Aspire.Hosting.Floci.ApiService";
const apiServiceProjectPath = path.join(appHostDirectory, "..", apiServiceProject, apiServiceProject + ".csproj");

const apiService = await builder.addProject("floci-api", apiServiceProjectPath)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .withReference(floci)
    .waitFor(floci);

// ── Compile-time coverage ─────────────────────────────────────────────────────
// Guards with false so these are type-checked but never executed.
// Covers the full exported API surface without requiring Docker in CI.
const includeCompileOnlyScenarios = false;

if (includeCompileOnlyScenarios) {

    // ── Custom socket path ────────────────────────────────────────────────────
    // Non-standard Docker installations (Podman, Rancher Desktop) expose the
    // socket at a different path; pass it explicitly.
    const _podman = await builder.addFloci('floci-podman');
    await _podman.withDockerSocket('/run/user/1000/podman/podman.sock');

    // ── Custom port and region ────────────────────────────────────────────────
    const _custom = await builder.addFloci('floci-custom', {
        port: 14566,
        defaultRegion: 'eu-west-1',
        defaultAccountId: '123456789012',
    });

    // ── Persistent storage — named volume ─────────────────────────────────────
    // Switches Floci from in-memory to persistent mode automatically.
    const _persistent = await builder.addFloci('floci-persistent');
    await _persistent.withDataVolume('floci-data');

    // ── Persistent storage — bind mount ───────────────────────────────────────
    const _mounted = await builder.addFloci('floci-mount');
    await _mounted.withDataBindMount('/tmp/floci-data');

    // ── Custom Quarkus config file ─────────────────────────────────────────────
    // Mounts application.yml read-only at /deployments/config/application.yml
    // inside the container so Quarkus merges it with built-in defaults on startup.
    const _configured = await builder.addFloci('floci-configured');
    await _configured.withConfigFile('./floci.yml');

    // ── TLS — Aspire development certificate ──────────────────────────────────
    // Call the standard Aspire API directly on the AddFloci return value.
    // The integration automatically sets FLOCI_TLS_ENABLED / FLOCI_TLS_CERT_PATH /
    // FLOCI_TLS_KEY_PATH when any certificate is configured.
    // Both HTTP and HTTPS are served on the same port (4566).
    // ConnectionStringExpression and AWS_ENDPOINT_URL automatically switch to https://.
    // Run `aspire certs trust` once to add the dev cert to your system trust store.
    //
    //   const _tls = await builder.addFloci('floci-tls');
    //   await _tls.withHttpsDeveloperCertificate();

    // ── Connection string / endpoint properties ───────────────────────────────
    //   connectionStringExpression → http://localhost:{port}  (host processes)
    //                               http://host.docker.internal:{port}  (containers)
    const _endpoint = await floci.primaryEndpoint();
    const _host = await floci.host();
    const _port = await floci.port();
    const _connectionString = await floci.connectionStringExpression();

    // ── WithReference — AWS env var injection ─────────────────────────────────
    //   Standard WithReference injects:
    //     ConnectionStrings__floci  = http://localhost:{port}
    //     AWS_ENDPOINT_URL          = http://localhost:{port}  (or host.docker.internal for containers)
    //     AWS_DEFAULT_REGION        = us-east-1
    //     AWS_ACCESS_KEY_ID         = test
    //     AWS_SECRET_ACCESS_KEY     = test
    const _project = await builder
        .addProject('api', '../FlociApi/FlociApi.csproj')
        .withReference(floci);

    const _container = await builder
        .addContainer('worker', 'myorg/worker')
        .withReference(floci);
}

await builder.build().run();

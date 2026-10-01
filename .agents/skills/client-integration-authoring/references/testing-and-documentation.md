# Client integration testing and documentation

## Tests

Cover the behavior the integration exposes:

- configuration-section binding;
- connection-string override;
- caller configuration override;
- default service registration;
- keyed registration and key isolation;
- expected service lifetime;
- missing required configuration;
- health-check registration and disable behavior;
- telemetry registration and disable behavior; and
- fluent builder features when present.

Use the nearest client integration test project as the structural reference. Add container-backed tests only when the client behavior cannot be proven without the service, and mark Docker-dependent tests with `[RequiresDocker]`.

When adding a test project, run:

```bash
./eng/testing/generate-test-list-for-workflow.sh --json
```

The reusable test matrix is `.github/workflows/tests.yaml`.

## README

Document only supported behavior:

1. Package purpose and installation.
2. Minimal `IHostApplicationBuilder` registration.
3. Expected connection string and configuration section.
4. Keyed registration if supported.
5. Health-check and telemetry behavior when present.
6. Fluent or Microsoft.Extensions.AI adapters when present.
7. A link to the matching example.

Keep snippets aligned with the compiled public API. Do not advertise a generic template capability that the integration does not implement.

# Example Roadmap

## Notes (Important)
- Community Toolkit currently targets **.NET 8**.
- Because of that, each new example should use the **classic 2-project AppHost structure** (AppHost + one consumer/service project), consistent with the approach used in `examples/perl/multi-resource`.
- Every example in this roadmap should include `OpenTelemetry::SDK` so telemetry flows into Aspire observability.
- Keep examples minimal: one focused feature per example, minimal dependencies, small scripts.

## Required Targets
- [ ] `cpan` example
- [ ] `cpanm` example
- [ ] `carton` example
- [ ] `script` entrypoint example
- [ ] `api` entrypoint example
- [ ] `exe` entrypoint example
- [ ] `docker publish` example

## Suggested Minimal Example Set

### 1) `examples/perl/cpan-script-minimal`
- Goal: Show baseline package installation with default `cpan` and a simple script.
- AppHost focus:
  - `AddPerlScript(...)`
  - `WithPackage("OpenTelemetry::SDK")`
- Runtime focus:
  - Script emits one trace/span periodically.

### 2) `examples/perl/cpanm-script-minimal`
- Goal: Show explicit `cpanm` flow and per-package install control.
- AppHost focus:
  - `AddPerlScript(...)`
  - `WithCpanMinus()`
  - `WithPackage("OpenTelemetry::SDK", force: false, skipTest: true)`
- Runtime focus:
  - Same tiny worker behavior as #1.

### 3) `examples/perl/carton-api-minimal`
- Goal: Show project-level deps from `cpanfile` with Carton.
- AppHost focus:
  - `AddPerlApi(...)`
  - `WithCarton()`
  - `WithProjectDependencies()`
  - `WithLocalLib()`
- Runtime focus:
  - `/health` + one business endpoint.
  - OpenTelemetry plugin + SDK present.

### 4) `examples/perl/api-http-https-minimal`
- Goal: Demonstrate dual endpoint wiring + TLS cert/key env mapping.
- AppHost focus:
  - `WithHttpEndpoint(name: "http", env: "PORT")`
  - `WithHttpsEndpoint(name: "https", env: "HTTPS_PORT")`
  - `WithHttpsCertificateConfiguration(ctx => ... TLS_CERT/TLS_KEY ... )`
- Runtime focus:
  - Mojolicious listener setup for HTTP+HTTPS.

### 5) `examples/perl/executable-minimal`
- Goal: Validate `AddPerlExecutable(...)` path and command handling.
- AppHost focus:
  - `AddPerlExecutable(...)`
  - Optional `WithArgs(...)` for executable args.
- Runtime focus:
  - Short-lived executable emits one OTEL span and exits cleanly.

### 6) `examples/perl/module-minimal`
- Goal: Demonstrate `AddPerlModule(...)` module entrypoint pattern.
- AppHost focus:
  - `AddPerlModule(...)`
  - `WithPackage("OpenTelemetry::SDK")`
- Runtime focus:
  - `Module::Name->run()` emits telemetry heartbeat.

### 7) `examples/perl/publish-docker-minimal`
- Goal: Show publish flow and generated Dockerfile behavior.
- AppHost focus:
  - Minimal API or script resource configured for publish.
  - Use package-manager-specific branch (`WithCarton` or `WithCpanMinus`) to validate publish output differences.
- Validation:
  - `dotnet publish` works.
  - Container starts and emits telemetry.

## Additional High-Value Targets

### 8) `examples/perl/perlbrew-environment-minimal`
- Goal: Validate `WithPerlbrewEnvironment(...)` pathing and env setup.
- Why: Environment portability and explicit runtime versioning are key real-world asks.

### 9) `examples/perl/certificate-trust-minimal`
- Goal: Demonstrate outbound TLS trust from Perl to another HTTPS resource.
- AppHost focus:
  - `WithPerlCertificateTrust()`
- Why: Complements HTTPS endpoint example with client trust behavior.

### 10) `examples/perl/mixed-dependency-strategy`
- Goal: One API with Carton + one worker with cpanm (small version of current multi-resource).
- Why: Shows recommended coexistence pattern without full demo complexity.

### 11) `examples/perl/service-discovery-client-minimal`
- Goal: .NET consumer uses `https+http://perl-api` and confirms HTTPS preference.
- Why: Makes service discovery contract explicit and easy to verify.

### 12) `examples/perl/failure-mode-deps-missing`
- Goal: Intentionally omit a dependency and show clear install/startup behavior.
- Why: Useful for docs and troubleshooting expectations.

## Recommended Build Order
1. `cpan-script-minimal`
2. `cpanm-script-minimal`
3. `carton-api-minimal`
4. `api-http-https-minimal`
5. `module-minimal`
6. `executable-minimal`
7. `publish-docker-minimal`
8. Remaining advanced targets

## Consistency Checklist (Apply To Every Example)
- [ ] Uses .NET 8-compatible AppHost pattern (classic 2-project structure).
- [ ] Includes `OpenTelemetry::SDK` dependency.
- [ ] Has one focused feature goal.
- [ ] Includes `README.md` with: purpose, run command, expected output, and troubleshooting.
- [ ] Includes `/health` endpoint for API examples.
- [ ] Avoids extra dependencies unless required by the feature.

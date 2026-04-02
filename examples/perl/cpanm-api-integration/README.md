# cpanm-api-integration

Minimal Perl API example for integration tests using `cpanm` and `Mojolicious::Lite`.

## Project
- `CpanmApiIntegration.AppHost` (Aspire AppHost)

## Perl focus
- Uses `AddPerlApi(...)`
- Uses `WithCpanMinus()`
- Uses `WithPackage("Mojolicious::Lite")`
- Uses HTTP endpoint wiring with `PORT`
- No HTTPS and no OpenTelemetry setup

## Endpoint
- `/`

## Run
```powershell
aspire run
```

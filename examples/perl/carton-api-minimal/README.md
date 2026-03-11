# carton-api-minimal

Minimal API example that uses Carton project dependencies and includes HTTPS wiring support.

## Projects
- `CartonApiMinimal.AppHost` (Aspire AppHost)
- `CartonApiMinimal.ServiceDefaults` (shared service defaults project)

## Perl focus
- Uses `AddPerlApi(...)`
- Uses `WithCarton()` + `WithProjectDependencies()` + `WithLocalLib()`
- Uses endpoint env wiring (`PORT`, `HTTPS_PORT`)
- Maps Aspire certificate paths to `TLS_CERT` / `TLS_KEY`
- Includes `OpenTelemetry::SDK` via `cpanfile`

## Endpoints
- `/`
- `/health`

## Run
```powershell
aspire run
```

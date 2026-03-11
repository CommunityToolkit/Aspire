# cpanm-script-minimal

Minimal example that uses `cpanm` explicitly with a Perl script resource.

## Projects
- `CpanmScriptMinimal.AppHost` (Aspire AppHost)
- `CpanmScriptMinimal.ServiceDefaults` (shared service defaults project)

## Perl focus
- Uses `AddPerlScript(...)`
- Uses `WithCpanMinus()`
- Uses `WithPackage("OpenTelemetry::SDK")`
- Emits simple spans/ticks from `scripts/Worker.pl`

## Run
```powershell
aspire run
```

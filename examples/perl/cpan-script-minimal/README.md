# cpan-script-minimal

Minimal example that uses `cpan` (default package manager) with a Perl script resource.

## Projects
- `CpanScriptMinimal.AppHost` (Aspire AppHost)
- `CpanScriptMinimal.ServiceDefaults` (shared service defaults project)

## Perl focus
- Uses `AddPerlScript(...)`
- Uses `WithPackage("OpenTelemetry::SDK")`
- Emits simple spans/ticks from `scripts/Worker.pl`

## Run
```powershell
aspire run
```

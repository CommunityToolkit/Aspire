# perlbrew-environment-minimal

Minimal script example that validates `WithPerlbrewEnvironment(...)` pathing and env setup.

## Projects
- `PerlbrewEnvironmentMinimal.AppHost` (Aspire AppHost)
- `PerlbrewEnvironmentMinimal.ServiceDefaults` (shared service defaults project)

## Perl focus
- Uses `AddPerlScript(...)`
- Uses `WithPerlbrewEnvironment("perl-5.42.0")`
- Validates worker runtime is `v5.42.x`

## Notes
- `WithPerlbrewEnvironment` is intended for systems with perlbrew installed.
- On Windows, this configuration is expected to be unsupported.
- `scripts/Worker.pl` enforces perl `v5.42.x` at startup and exits with an error on other versions.

## Version validation
1. Ensure perlbrew has a `perl-5.42.x` installation and your system perl is `5.38.x`.
2. Then run the Worker.pl script which will tell you what version you're running.

Expected: `PASS: running on expected perl series v5.42...`

4. Run `aspire run` and confirm `perlbrew-worker` stays healthy with logs showing:
	- `perl version validated: v5.38...`
	- recurring `tick`

This validates both the explicit script check and Aspire-managed runtime selection from `WithPerlbrewEnvironment("perl-5.42.0")`.

## Run
```powershell
aspire run
```

# perlbrew-environment-minimal

Minimal script example that validates `WithPerlbrewEnvironment(...)` pathing and env setup.

## Projects
- `PerlbrewEnvironmentMinimal.AppHost` (Aspire AppHost)
- `PerlbrewEnvironmentMinimal.ServiceDefaults` (shared service defaults project)

## Perl focus
- Uses `AddPerlScript(...)`
- Uses `WithPerlbrewEnvironment("perl-5.38.0")`
- Validates worker runtime is `v5.38.x`

## Notes
- `WithPerlbrewEnvironment` is intended for systems with perlbrew installed.
- On Windows, this configuration is expected to be unsupported.
- `scripts/Worker.pl` enforces perl `v5.38.x` at startup and exits with an error on other versions.

## Version validation
1. Ensure perlbrew has a `perl-5.38.x` installation and your system perl is `5.42.x`.
2. From this folder, run the standalone check with default perl:

```powershell
perl .\scripts\ValidatePerlVersion.pl
```

Expected: `FAIL: expected perl v5.38.x but got v5.42...`

3. Run the same check through perlbrew:

```powershell
perlbrew exec --with perl-5.38.0 perl .\scripts\ValidatePerlVersion.pl
```

Expected: `PASS: running on expected perl series v5.38...`

4. Run `aspire run` and confirm `perlbrew-worker` stays healthy with logs showing:
	- `perl version validated: v5.38...`
	- recurring `tick`

This validates both the explicit script check and Aspire-managed runtime selection from `WithPerlbrewEnvironment("perl-5.38.0")`.

## Run
```powershell
aspire run
```

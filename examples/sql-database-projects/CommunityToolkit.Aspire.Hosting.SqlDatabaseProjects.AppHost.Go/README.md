# SQL Database Projects Go AppHost

This example mirrors the adjacent TypeScript AppHost, including both a managed SQL database and an existing connection string.

With Go 1.26 or later and the Aspire CLI version pinned in `aspire.config.json`, run:

```shell
aspire restore --non-interactive
go test ./...
go vet ./...
aspire publish --list-steps --non-interactive
```

Run the application with `aspire run`. Configure `ConnectionStrings:Aspire` for the existing database before running.

## Preview limitation

SDK generation and Go compilation succeed with `17.1.0-preview.1.26559.8`. On the Windows prebuilt AppHost server, model construction currently fails in `SqlProjectBuilderExtensions` when `MSBuildLocator.RegisterDefaults()` cannot load the native `hostfxr` library. Adding the installed `hostfxr.dll` directory to `PATH` did not resolve this failure. The example retains both SQL project resources rather than omitting them to bypass the prerequisite failure.

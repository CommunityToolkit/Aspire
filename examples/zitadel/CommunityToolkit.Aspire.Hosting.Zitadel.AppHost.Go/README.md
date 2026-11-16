# Zitadel Go AppHost

This example mirrors the adjacent TypeScript AppHost, including explicit admin parameters, a managed database, and a second Zitadel instance using an existing database.

With Go 1.26 or later and the Aspire CLI version pinned in `aspire.config.json`, run:

```shell
aspire restore --non-interactive
go test ./...
go vet ./...
aspire publish --list-steps --non-interactive
```

Run the application with `aspire run` once the preview limitation below is resolved. The fixed credentials match the TypeScript example and are for local demonstration only.

## Preview limitation

SDK generation and Go compilation succeed with `17.1.0-preview.1.26559.8`, but model construction fails at `AddZitadel`. The generated options expose optional resource arguments as pointers to interfaces (`*ParameterResource`), then serialize those pointers without unwrapping their resource handles. The AppHost server rejects `username` with `INVALID_ARGUMENT`, reporting that `IResourceBuilder` requires an `AspireDto` attribute.

This example preserves the explicit username, password, and master-key parameters. Resolving the failure requires an Aspire Go SDK serialization fix; do not edit the generated `.aspire/modules` files or drop the parameters to hide it.

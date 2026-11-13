# Flare Go AppHost

This example mirrors the adjacent TypeScript AppHost using the pinned Aspire 17
preview Go SDK. Run `aspire restore --apphost apphost.go`, then `go test ./...`
and `go vet ./...` to validate the generated SDK and example.

The current preview SDK does not serialize optional resource-builder handles
correctly. Model construction fails at `WithPublicUrls` when passing the dashboard
URL parameter, with `INVALID_ARGUMENT` for `dashboardUrl`. The example retains
the parameter-based configuration for parity; its integration test validates
compilation only until the upstream SDK supports this argument.

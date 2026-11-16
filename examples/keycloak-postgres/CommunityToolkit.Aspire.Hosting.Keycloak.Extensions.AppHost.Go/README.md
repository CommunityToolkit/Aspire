# Keycloak PostgreSQL Go AppHost

This example mirrors the adjacent TypeScript AppHost using the pinned Aspire 17
preview Go SDK. Run `aspire restore --apphost apphost.go`, then `go test ./...`
and `go vet ./...` to validate the generated SDK and example.

`AddKeycloak` is not exported by the underlying hosting package. As in the
TypeScript example, `WithPostgres` receives compile-only coverage and the runtime
graph contains PostgreSQL and its database.

The current preview SDK also fails to serialize the optional PostgreSQL username
and password resource-builder handles. Model construction reports
`INVALID_ARGUMENT` for `userName`. The example preserves these parameters rather
than substituting plain-text credentials; its integration test validates
compilation only until the upstream SDK supports these arguments.

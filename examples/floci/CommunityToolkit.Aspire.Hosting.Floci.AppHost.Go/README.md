# Floci Go AppHost

This example mirrors the adjacent TypeScript AppHost, including all three cloud
emulators, their shared UI, the API service, and the Service Bus AMQP probe.
It requires the pinned Aspire 17 preview CLI, Go 1.26 or later, and Docker.

From this directory, run:

```shell
aspire restore --apphost apphost.go
go test ./...
go vet ./...
aspire run --apphost apphost.go
```

The Service Bus emulator supports run mode only, so publishing this development
graph is intentionally unsupported. The integration test validates compilation
without constructing a publish-mode model. The checked-in TypeScript example
remains the source of the equivalent runtime scenario.

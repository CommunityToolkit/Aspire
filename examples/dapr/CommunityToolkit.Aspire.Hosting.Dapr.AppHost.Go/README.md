# Dapr Go AppHost

This is the Go counterpart of the adjacent TypeScript AppHost. It connects three
.NET services to Dapr state and pub/sub components and makes services B and C
wait for service A.

Component references are configured on the sidecars through the exported
`ConfigureDaprSidecar` callback. This preserves the intended graph without using
the generic project `WithReference`, which does not accept Dapr components.

Use the Aspire 17 preview CLI matching `aspire.config.json`, Go 1.26 or later,
the repository's .NET SDK, and the Dapr CLI. From this directory:

```shell
aspire restore --non-interactive
go build ./...
go vet ./...
aspire publish --non-interactive
aspire start --isolated --non-interactive
```

Local execution additionally requires Docker and an initialized Dapr runtime.
The Dapr CLI must be on `PATH` even when publishing the model.

# Deno Go AppHost

This is the Go counterpart of the adjacent TypeScript AppHost. It runs the
existing Vite and Oak examples with the same tasks, Deno permissions, HTTP
endpoints, health checks, and package installation.

Use the Aspire 17 preview CLI matching `aspire.config.json` and Go 1.26 or later.
From this directory:

```shell
aspire restore --non-interactive
go build ./...
go vet ./...
aspire publish --non-interactive
aspire start --isolated --non-interactive
```

Running the applications additionally requires Deno on `PATH`.

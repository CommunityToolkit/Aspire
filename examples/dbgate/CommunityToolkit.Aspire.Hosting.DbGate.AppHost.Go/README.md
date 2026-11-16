# DbGate Go AppHost

This is the Go counterpart of the adjacent TypeScript AppHost. It configures a
DbGate container on port 3310 with a temporary data bind mount and accesses its
primary endpoint.

Use the Aspire 17 preview CLI matching `aspire.config.json` and Go 1.26 or later.
From this directory:

```shell
aspire restore --non-interactive
go build ./...
go vet ./...
aspire publish --non-interactive
aspire start --isolated --non-interactive
```

Running the container requires Docker. Keep `ASPIRE_COMPILE_ONLY` unset for
execution: its optional volume call only covers the exported API, because the
volume and bind mount would otherwise overlap.

# ActiveMQ Go AppHost

This is the Go counterpart of the adjacent TypeScript AppHost. It configures
Classic and Artemis brokers, explicit credentials, ports, volumes, bind mounts,
and exported endpoint properties.

Use the Aspire 17 preview CLI matching `aspire.config.json` and Go 1.26 or later.
From this directory:

```shell
aspire restore --non-interactive
go build ./...
go vet ./...
aspire start --isolated --non-interactive
```

Running the brokers requires Docker.

## Preview SDK limitation

With Aspire `17.1.0-preview.1.26559.8`, compilation succeeds but model execution
fails at `addActiveMQ` with `INVALID_ARGUMENT` for `userName`. The generated Go
options use `*ParameterResource` for optional resource parameters, but their
serializer does not unwrap the pointer to the interface into a resource handle.
The explicit credentials are retained for parity rather than removing them or
modifying generated SDK files.

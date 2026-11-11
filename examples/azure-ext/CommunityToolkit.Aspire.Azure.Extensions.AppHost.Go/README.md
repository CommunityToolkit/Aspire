# Azure extensions Go AppHost

This is the Go counterpart of the adjacent TypeScript AppHost. It configures
Azurite with blob, queue, and table ports 27000-27002, a data volume, and Azure
Storage Explorer for the blob resource.

Use the Aspire 17 preview CLI matching `aspire.config.json` and Go 1.26 or later.
From this directory:

```shell
aspire restore --non-interactive
go build ./...
go vet ./...
aspire start --isolated --non-interactive
```

Running the emulator and explorer requires Docker. `aspire publish
--non-interactive` validates the publish model without provisioning Azure
resources.

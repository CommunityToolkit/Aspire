# Data API builder Go AppHost

This is the Go counterpart of the adjacent TypeScript AppHost. It configures SQL
Server and the `trek` database, with two Data API builder resources referencing
the database and waiting for SQL Server. The second resource uses port 5001 and
two configuration files.

The Go example reuses the configuration files and SQL initialization scripts
from the adjacent TypeScript AppHost rather than duplicating them.

Use the Aspire 17 preview CLI matching `aspire.config.json` and Go 1.26 or later.
From this directory:

```shell
aspire restore --non-interactive
go build ./...
go vet ./...
aspire publish --non-interactive
aspire start --isolated --non-interactive
```

Running the containers requires Docker.

# Elasticsearch extensions Go AppHost

This is the Go counterpart of the adjacent TypeScript AppHost. It provides
compile-time coverage of `WithElasticvue`, its configuration callback, and
`WithHostPort(9280)`.

The Elasticsearch creation API is not exported by the preview SDK. Like the
TypeScript example, this AppHost deliberately creates no resources; the helper
functions validate the exported extension signatures without invoking them.

Use the Aspire 17 preview CLI matching `aspire.config.json` and Go 1.26 or later.
From this directory:

```shell
aspire restore --non-interactive
go build ./...
go vet ./...
aspire publish --non-interactive
```

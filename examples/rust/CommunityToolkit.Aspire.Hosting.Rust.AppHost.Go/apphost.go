package main

import (
	"apphost/modules/aspire"
	"log"
	"os"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	rust := builder.AddRustApp("rust-app", "../actix_api")
	rust.WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")})
	rust.WithExternalHttpEndpoints()
	rust.WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/")})
	check(rust.Err())

	if os.Getenv("ENABLE_BACON") == "1" {
		bacon := builder.AddBaconApp("bacon-app", "../actix_api")
		bacon.WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")})
		check(bacon.Err())
	}

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

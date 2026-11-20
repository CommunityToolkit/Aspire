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

	bacon := builder.AddBaconApp("bacon-app", "../actix_api", &aspire.AddBaconAppOptions{
		Args: []string{"--headless", "run-long"},
	})
	bacon.WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")})
	bacon.WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/ping")})
	if os.Getenv("ENABLE_BACON") != "1" {
		bacon.WithExplicitStart()
	}
	check(bacon.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

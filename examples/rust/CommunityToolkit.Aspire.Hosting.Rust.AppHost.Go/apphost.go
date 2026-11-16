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

	if os.Getenv("RUST_ARGS_VALIDATION") == "1" {
		withArgs := builder.AddRustApp("rust-app-with-args", "../actix_api", &aspire.AddRustAppOptions{
			Args: []string{"--", "--help"},
		})
		withArgs.WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")})
		check(withArgs.Err())
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

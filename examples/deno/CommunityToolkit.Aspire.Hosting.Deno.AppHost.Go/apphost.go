package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	vite := builder.AddDenoTask("vite-demo", &aspire.AddDenoTaskOptions{TaskName: aspire.StringPtr("dev")}).
		WithDenoPackageInstallation().
		WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")}).
		WithEndpoint().
		WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/")})
	check(vite.Err())
	oak := builder.AddDenoApp("oak-demo", "main.ts", &aspire.AddDenoAppOptions{
		PermissionFlags: []string{"--allow-env", "--allow-net"},
	}).WithHttpEndpoint(&aspire.WithHttpEndpointOptions{Env: aspire.StringPtr("PORT")}).
		WithEndpoint().
		WithHttpHealthCheck(&aspire.WithHttpHealthCheckOptions{Path: aspire.StringPtr("/health")})
	check(oak.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

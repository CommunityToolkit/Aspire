package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	flyway := builder.AddFlyway("flyway", "../CommunityToolkit.Aspire.Hosting.Flyway.AppHost.TypeScript/migrations")
	flyway.WithArgs([]string{"-v"})
	if err := flyway.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	telemetry := builder.AddFlyway("flyway-telemetry", "../CommunityToolkit.Aspire.Hosting.Flyway.AppHost.TypeScript/migrations")
	telemetry.WithTelemetryOptIn().WithArgs([]string{"-v"})
	if err := telemetry.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

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

	collector := builder.AddOpenTelemetryCollector("collector")
	collector.WithConfig("../CommunityToolkit.Aspire.Hosting.OpenTelemetryCollector.AppHost.TypeScript/otel-config.yaml")
	collector.WithAppForwarding()
	grpc := collector.GrpcEndpoint()
	http := collector.HttpEndpoint()
	if err := collector.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := grpc.EndpointName(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if _, err := http.EndpointName(); err != nil {
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

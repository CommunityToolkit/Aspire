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

	clickhouse := builder.AddClickHouse("clickhouse")
	redis := builder.AddRedis("redis")
	ingestKey := builder.AddParameter("flare-ingest-key", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("local-dev-ingest-key"), Secret: aspire.BoolPtr(true),
	})
	dashboardURL := builder.AddParameter("flare-dashboard-url", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("http://localhost:3000"),
	})
	flare := builder.AddFlare("flare", clickhouse, redis, &aspire.AddFlareOptions{
		DashboardPort: aspire.Float64Ptr(3000),
	})
	flare.WithDataVolume().
		WithIngestPorts(&aspire.WithIngestPortsOptions{GrpcPort: aspire.Float64Ptr(4317), HttpPort: aspire.Float64Ptr(4318)}).
		WithFlareImageTag("0.6").
		WithPublicUrls(&aspire.WithPublicUrlsOptions{DashboardUrl: &dashboardURL}).
		WithApiKey(ingestKey)
	check(flare.OtlpGrpcEndpoint().Err())
	check(flare.OtlpHttpEndpoint().Err())
	protocol := aspire.OtlpProtocolHttpProtobuf
	api := builder.AddDotnetProject("apiservice", "../CommunityToolkit.Aspire.Hosting.Flare.ApiService/CommunityToolkit.Aspire.Hosting.Flare.ApiService.csproj").
		WithFlareOtlpExporter(flare, &aspire.WithFlareOtlpExporterOptions{Protocol: &protocol}).
		WaitFor(flare)
	if err := api.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := flare.Err(); err != nil {
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

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

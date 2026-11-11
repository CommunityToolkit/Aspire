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

	duckdb := builder.AddDuckDB("analytics")
	if err := duckdb.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	api := builder.AddDotnetProject("api", "../CommunityToolkit.Aspire.DuckDB.Api/CommunityToolkit.Aspire.DuckDB.Api.csproj")
	api.WithReference(duckdb)
	if err := api.Err(); err != nil {
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

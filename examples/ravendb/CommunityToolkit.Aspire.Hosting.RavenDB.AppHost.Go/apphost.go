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

	ravendb := builder.AddRavenDB("ravendb")
	database := ravendb.AddDatabase("ravenDatabase")
	if err := ravendb.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := database.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	api := builder.AddDotnetProject("apiservice", "../CommunityToolkit.Aspire.Hosting.RavenDB.ApiService/CommunityToolkit.Aspire.Hosting.RavenDB.ApiService.csproj")
	api.WithReference(ravendb).WaitFor(ravendb)
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

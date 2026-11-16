package main

import (
	"apphost/modules/aspire"
	"log"
	"path/filepath"
)

func main() {
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	appHostDirectory, err := builder.AppHostDirectory()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	csharpAppHost := filepath.Join(appHostDirectory, "..", "CommunityToolkit.Aspire.Hosting.Squad.AppHost")

	research := builder.AddSquad("research-squad", &aspire.AddSquadOptions{
		TeamRoot: aspire.StringPtr(filepath.Join(csharpAppHost, "research-squad")),
	})
	if err := research.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	development := builder.AddSquad("dev-squad", &aspire.AddSquadOptions{
		TeamRoot: aspire.StringPtr(filepath.Join(csharpAppHost, "dev-squad")),
	})
	if err := development.Err(); err != nil {
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

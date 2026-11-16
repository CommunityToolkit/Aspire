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
	flagsPath := "../CommunityToolkit.Aspire.Hosting.Flagd.AppHost.TypeScript/flags"

	flagd := builder.AddFlagd("flagd", &aspire.AddFlagdOptions{
		Port:      aspire.Float64Ptr(18013),
		OfrepPort: aspire.Float64Ptr(18016),
	})
	flagd.WithBindFileSync(flagsPath)
	check(flagd.PrimaryEndpoint().Err())
	check(flagd.Host().Err())
	check(flagd.Port().Err())
	check(flagd.GetEndpoint("health").Err())
	check(flagd.GetEndpoint("ofrep").Err())
	_ = flagd.UriExpression()
	_ = flagd.ConnectionStringExpression()
	if err := flagd.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	defaults := builder.AddFlagd("flagd-default")
	defaults.WithBindFileSync(flagsPath, &aspire.WithBindFileSyncOptions{Filename: aspire.StringPtr("custom-flagd.json")})
	check(defaults.PrimaryEndpoint().Err())
	check(defaults.Host().Err())
	check(defaults.Port().Err())
	check(defaults.GetEndpoint("health").Err())
	check(defaults.GetEndpoint("ofrep").Err())
	_ = defaults.UriExpression()
	_ = defaults.ConnectionStringExpression()
	if err := defaults.Err(); err != nil {
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

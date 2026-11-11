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

	redpanda := builder.AddRedPanda("redpanda")
	redpanda.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("redpanda-data")})
	_ = redpanda.PrimaryEndpoint()
	_ = redpanda.Host()
	_ = redpanda.Port()
	_ = redpanda.ConnectionStringExpression()
	if err := redpanda.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	pinned := builder.AddRedPanda("redpanda-pinned", &aspire.AddRedPandaOptions{Port: aspire.Float64Ptr(9092)})
	if err := pinned.Err(); err != nil {
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

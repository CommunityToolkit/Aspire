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

	chroma := builder.AddChroma("chroma")
	chroma.WithHostPort(aspire.Float64Ptr(18080))
	chroma.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("chroma-data")})
	_ = chroma.PrimaryEndpoint()
	_ = chroma.Host()
	_ = chroma.Port()
	_ = chroma.ConnectionStringExpression()
	_ = chroma.UriExpression()
	if err := chroma.Err(); err != nil {
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

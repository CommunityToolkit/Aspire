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
	check(chroma.PrimaryEndpoint().Err())
	check(chroma.Host().Err())
	check(chroma.Port().Err())
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

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

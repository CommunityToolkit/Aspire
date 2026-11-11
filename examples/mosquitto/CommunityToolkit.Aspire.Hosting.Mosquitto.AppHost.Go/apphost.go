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

	mqtt := builder.AddMosquitto("mqtt")
	mqtt.WithDataVolume(&aspire.WithDataVolumeOptions{Name: aspire.StringPtr("mosquitto-data")})
	_ = mqtt.PrimaryEndpoint()
	_ = mqtt.Host()
	_ = mqtt.Port()
	_ = mqtt.ConnectionStringExpression()
	_ = mqtt.UriExpression()
	if err := mqtt.Err(); err != nil {
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

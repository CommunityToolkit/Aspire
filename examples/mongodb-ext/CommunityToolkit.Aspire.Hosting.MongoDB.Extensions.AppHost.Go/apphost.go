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

	mongo := builder.AddMongoDB("mongo")
	mongo.WithDbGate()
	catalog := mongo.AddDatabase("catalog")
	mongo.WithDataVolume()
	if err := mongo.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := catalog.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	named := builder.AddMongoDB("mongo-named")
	named.WithDbGate(&aspire.WithDbGateOptions{ContainerName: aspire.StringPtr("mongo-ui")})
	orders := named.AddDatabase("orders", &aspire.AddDatabaseOptions{DatabaseName: aspire.StringPtr("orders-db")})
	if err := named.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := orders.Err(); err != nil {
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

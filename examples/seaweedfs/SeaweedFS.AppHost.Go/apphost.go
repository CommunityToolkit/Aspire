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

	s3 := builder.AddSeaweedFS("typescript-seaweedfs-s3")
	s3.WithS3()
	s3.WithDataVolume()
	if err := s3.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	filer := builder.AddSeaweedFS("typescript-seaweedfs-filer")
	filer.WithFiler()
	if err := filer.Err(); err != nil {
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

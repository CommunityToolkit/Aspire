package main

import (
	"apphost/modules/aspire"
	"log"
	"os"
)

func main() {
	bindMountPath, err := os.MkdirTemp("", "lavinmq-")
	if err != nil {
		log.Fatal(err)
	}
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	volume := builder.AddLavinMQ("volume-broker", &aspire.AddLavinMQOptions{
		AmqpPort:       aspire.Float64Ptr(35672),
		ManagementPort: aspire.Float64Ptr(35673),
	})
	volume.WithDataVolume("lavinmq-volume")
	check(volume.PrimaryEndpoint().Err())
	check(volume.Host().Err())
	check(volume.Port().Err())
	_ = volume.UriExpression()
	_ = volume.ConnectionStringExpression()
	if err := volume.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	mounted := builder.AddLavinMQ("bind-broker", &aspire.AddLavinMQOptions{
		AmqpPort:       aspire.Float64Ptr(35674),
		ManagementPort: aspire.Float64Ptr(35675),
	})
	mounted.WithDataBindMount(bindMountPath)
	if err := mounted.Err(); err != nil {
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

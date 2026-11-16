package main

import (
	"apphost/modules/aspire"
	"log"
	"os"
)

func main() {
	bindMountPath, err := os.MkdirTemp("", "mailpit-")
	if err != nil {
		log.Fatal(err)
	}
	builder, err := aspire.CreateBuilder()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	mailpit := builder.AddMailPit("mailpit", &aspire.AddMailPitOptions{
		HttpPort: aspire.Float64Ptr(18025),
		SmtpPort: aspire.Float64Ptr(11025),
	})
	mailpit.WithDataVolume("mailpit-data")
	check(mailpit.PrimaryEndpoint().Err())
	check(mailpit.Host().Err())
	check(mailpit.Port().Err())
	_ = mailpit.UriExpression()
	_ = mailpit.ConnectionStringExpression()
	if err := mailpit.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	defaults := builder.AddMailPit("mailpit-default")
	defaults.WithDataBindMount(bindMountPath)
	check(defaults.PrimaryEndpoint().Err())
	check(defaults.Host().Err())
	check(defaults.Port().Err())
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

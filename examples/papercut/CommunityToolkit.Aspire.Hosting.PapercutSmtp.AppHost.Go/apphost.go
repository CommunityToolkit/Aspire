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

	papercut := builder.AddPapercutSmtp("papercut", &aspire.AddPapercutSmtpOptions{
		HttpPort: aspire.Float64Ptr(8080),
		SmtpPort: aspire.Float64Ptr(2525),
	})
	_ = papercut.Host()
	_ = papercut.Port()
	_ = papercut.UriExpression()
	_ = papercut.ConnectionStringExpression()
	if err := papercut.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	defaults := builder.AddPapercutSmtp("papercut-default")
	_ = defaults.Host()
	_ = defaults.Port()
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

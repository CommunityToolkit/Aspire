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

	defaultK6 := builder.AddK6("k6-default")
	browserK6 := builder.AddK6("k6-browser", &aspire.AddK6Options{
		EnableBrowserExtensions: aspire.BoolPtr(true), Port: aspire.Float64Ptr(6566),
	})
	for _, resource := range []aspire.K6Resource{defaultK6, browserK6} {
		resource.WithBindMount("../CommunityToolkit.Aspire.Hosting.k6.AppHost.TypeScript/scripts", "/scripts", &aspire.WithBindMountOptions{IsReadOnly: aspire.BoolPtr(true)})
	}
	defaultK6.WithScript("/scripts/main.js").WithK6OtlpEnvironment()
	browserK6.WithScript("/scripts/main.js", &aspire.WithScriptOptions{
		VirtualUsers: aspire.Float64Ptr(5), Duration: aspire.StringPtr("45s"),
	}).WithK6OtlpEnvironment()
	for _, resource := range []aspire.K6Resource{defaultK6, browserK6} {
		if err := resource.PrimaryEndpoint().Err(); err != nil {
			log.Fatal(aspire.FormatError(err))
		}
		if err := resource.Err(); err != nil {
			log.Fatal(aspire.FormatError(err))
		}
	}

	app, err := builder.Build()
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	if err := app.Run(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

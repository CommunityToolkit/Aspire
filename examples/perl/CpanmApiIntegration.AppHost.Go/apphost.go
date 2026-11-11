package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	perlAPI := builder.AddPerlApi("perl-api", ".", "../scripts/API.pl")
	perlAPI.WithCpanMinus()
	perlAPI.WithPackage("Mojolicious::Lite", &aspire.WithPackageOptions{
		Force: aspire.BoolPtr(true), SkipTest: aspire.BoolPtr(true),
	})
	perlAPI.WithLocalLib(&aspire.WithLocalLibOptions{Path: aspire.StringPtr("local")})
	perlAPI.WithHttpEndpoint(&aspire.WithHttpEndpointOptions{
		Name: aspire.StringPtr("http"), Env: aspire.StringPtr("PORT"),
	})
	check(perlAPI.Err())

	driver := builder.AddPerlScript("perl-driver", "../scripts", "driver.pl")
	driver.WithEnvironment("API_URL", perlAPI.GetEndpoint("http"))
	driver.WithReference(perlAPI).WaitFor(perlAPI)
	check(driver.Err())

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

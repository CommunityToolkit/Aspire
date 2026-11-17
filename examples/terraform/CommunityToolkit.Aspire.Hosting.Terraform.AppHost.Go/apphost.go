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

	manualStart := func(command aspire.TerraformCliResource) { command.WithExplicitStart() }
	terraform := builder.AddTerraformEnvironment("sample", "../module").
		WithTfVar("message", "Hello from Aspire").
		WithInit(&aspire.WithInitOptions{Configure: manualStart}).
		WithPlan(&aspire.WithPlanOptions{Configure: manualStart}).
		WithApply(&aspire.TerraformEnvironmentResourceWithApplyOptions{Configure: manualStart}).
		WithDestroy()
	if err := terraform.Err(); err != nil {
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

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

	vllm := builder.AddVLLM("vllm").
		WithGPUSupport().
		WithDataVolume().
		WithModel("Qwen/Qwen3-0.6B").
		WithServedModelName("qwen")
	_ = vllm.ConnectionStringExpression()
	if err := vllm.Err(); err != nil {
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

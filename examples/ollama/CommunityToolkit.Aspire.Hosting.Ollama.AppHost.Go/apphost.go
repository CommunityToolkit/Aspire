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

	ollama := builder.AddOllama("ollama")
	ollama.WithOpenWebUI()
	phi3 := ollama.AddNamedModel("phi3", "phi3")
	llama := ollama.AddHuggingFaceModel("llama", "bartowski/Llama-3.2-1B-Instruct-GGUF:IQ4_XS")
	if err := ollama.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	_ = phi3.ConnectionStringExpression()
	if err := phi3.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	_ = llama.ConnectionStringExpression()
	if err := llama.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}

	ollama2 := builder.AddOllama("ollama2")
	ollama2.WithDataVolume()
	ollama2.WithOpenWebUI()
	tinyllama := ollama2.AddNamedModel("tinyllama", "tinyllama")
	if err := ollama2.Err(); err != nil {
		log.Fatal(aspire.FormatError(err))
	}
	_ = tinyllama.ConnectionStringExpression()
	if err := tinyllama.Err(); err != nil {
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

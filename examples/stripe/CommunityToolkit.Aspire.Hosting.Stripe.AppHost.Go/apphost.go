package main

import (
	"apphost/modules/aspire"
	"log"
)

func main() {
	builder, err := aspire.CreateBuilder()
	check(err)

	apiKey := builder.AddParameter("stripe-api-key", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("sk_test_123"), Secret: aspire.BoolPtr(true),
	})
	override := builder.AddParameter("stripe-api-key-override", &aspire.AddParameterOptions{
		Value: aspire.StringPtr("sk_test_override"), Secret: aspire.BoolPtr(true),
	})
	target := builder.AddContainer("webhook-target", "nginx:alpine")
	target.WithHttpEndpoint(&aspire.WithHttpEndpointOptions{
		TargetPort: aspire.Float64Ptr(80), Name: aspire.StringPtr("http"),
	})
	external := builder.AddExternalService("external-target", "http://localhost:5082")
	consumer := builder.AddContainer("consumer", "nginx:alpine")
	check(apiKey.Err())
	check(override.Err())
	check(target.Err())
	check(external.Err())
	check(consumer.Err())

	stripe := builder.AddStripe("stripe", apiKey)
	stripe.WithListen(target, &aspire.WithListenOptions{
		WebhookPath: aspire.StringPtr("/webhooks/stripe"), Events: []string{"payment_intent.created"},
	})
	stripe.WithApiKey(override)
	check(stripe.Err())

	stripeExternal := builder.AddStripe("stripe-external", apiKey)
	stripeExternal.WithListenExternalService(external, &aspire.WithListenExternalServiceOptions{
		WebhookPath: aspire.StringPtr("/webhooks/external"), Events: []string{"payment_intent.created", "charge.succeeded"},
	})
	check(stripeExternal.Err())

	consumer.WithStripeReference(stripe, &aspire.WithStripeReferenceOptions{
		WebhookSigningSecretEnvVarName: aspire.StringPtr("CUSTOM_STRIPE_SECRET"),
	})
	check(consumer.Err())
	_, err = stripe.WebhookSigningSecret()
	check(err)

	app, err := builder.Build()
	check(err)
	check(app.Run())
}

func check(err error) {
	if err != nil {
		log.Fatal(aspire.FormatError(err))
	}
}

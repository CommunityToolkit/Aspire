import { createBuilder } from './.modules/aspire.js';

const builder = await createBuilder();

const stripeApiKey = await builder.addParameterWithValue("stripe-api-key", "sk_test_123", { secret: true });
const stripeApiKeyOverride = await builder.addParameterWithValue("stripe-api-key-override", "sk_test_override", { secret: true });

const webhookTarget = await builder
    .addContainer("webhook-target", "nginx:alpine")
    .withHttpEndpoint({ targetPort: 80, name: "http" });

const externalTarget = await builder.addExternalService("external-target", "http://localhost:5082");
const consumer = await builder.addContainer("consumer", "nginx:alpine");

const stripe = await builder.addStripe("stripe", stripeApiKey);
await stripe.withListen(webhookTarget, {
    webhookPath: "/webhooks/stripe",
    events: ["payment_intent.created"]
});
await stripe.withApiKey(stripeApiKeyOverride);

const stripeExternal = await builder.addStripe("stripe-external", stripeApiKey);
await stripeExternal.withListenExternalService(externalTarget, {
    webhookPath: "/webhooks/external",
    events: ["payment_intent.created", "charge.succeeded"]
});

await consumer.withStripeReference(stripe, {
    webhookSigningSecretEnvVarName: "CUSTOM_STRIPE_SECRET"
});

const _webhookSigningSecret: string = await stripe.webhookSigningSecret.get();

await builder.build().run();

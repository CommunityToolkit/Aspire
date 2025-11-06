var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.API>("api");

// Forward Stripe webhooks to the API's webhook endpoint
var stripe = builder.AddStripe("stripe")
    .WithListen("http://localhost:5082/payments/stripe-webhook");

// The API will receive the webhook signing secret via STRIPE_WEBHOOK_SECRET environment variable
api.WithReference(stripe);

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api>("api");

// Forward Stripe webhooks to the API's webhook endpoint
var stripe = builder.AddStripe("stripe")
    .WithListen(api);

// The API will receive the webhook signing secret via STRIPE_WEBHOOK_SECRET environment variable
api.WithReference(stripe);

builder.Build().Run();

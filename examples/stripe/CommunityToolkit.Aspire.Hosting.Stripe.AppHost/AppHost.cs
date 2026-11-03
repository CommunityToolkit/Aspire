var builder = DistributedApplication.CreateBuilder(args);

#pragma warning disable ASPIREDOTNETPROJECT001
var api = builder.AddDotnetProject("api", new Projects.CommunityToolkit_Aspire_Hosting_Stripe_Api().ProjectPath);
#pragma warning restore ASPIREDOTNETPROJECT001

// Provide a development default; override via configuration in real projects.
var stripeApiKey = builder.AddParameter("stripe-api-key", secret: true);

// Forward Stripe webhooks to the API's webhook endpoint
var stripe = builder.AddStripe("stripe", stripeApiKey)
    .WithListen(api);

// The API will receive the webhook signing secret via STRIPE_WEBHOOK_SECRET environment variable
api.WithReference(stripe);

builder.Build().Run();

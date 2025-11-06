# CommunityToolkit.Aspire.Hosting.Stripe library

Provides extension methods and resource definitions for a .NET Aspire AppHost to configure the Stripe CLI for local webhook forwarding and testing.

## Getting Started

### Prerequisites

The Stripe CLI must be installed on your machine. You can install it by following the [official Stripe CLI installation guide](https://stripe.com/docs/stripe-cli#install).

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Stripe
```

### Example usage

Then, in the _Program.cs_ file of your AppHost project, add the Stripe CLI and configure it to forward webhooks to your API:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var stripe = builder.AddStripe("stripe")
    .WithListen("http://localhost:5082/payments/stripe-webhook");

var api = builder.AddProject<Projects.API>("api")
    .WithReference(stripe);

builder.Build().Run();
```

This will:
1. Start the Stripe CLI listening for webhook events
2. Forward all webhook events to `http://localhost:5082/payments/stripe-webhook`
3. Make the webhook signing secret available to the API project via the `STRIPE_WEBHOOK_SECRET` environment variable

### Forwarding to an Aspire endpoint

You can also forward webhooks to an Aspire endpoint reference:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.API>("api")
    .WithHttpEndpoint(port: 5082, name: "http");

var stripe = builder.AddStripe("stripe")
    .WithListen(api.GetEndpoint("http"), events: "payment_intent.created,charge.succeeded");

api.WithReference(stripe);

builder.Build().Run();
```

### Using a custom environment variable for the webhook secret

By default, the webhook signing secret is exposed as `STRIPE_WEBHOOK_SECRET`. You can customize this:

```csharp
var api = builder.AddProject<Projects.API>("api")
    .WithReference(stripe, webhookSigningSecretEnvVarName: "STRIPE_SECRET");
```

### Configuring API key

You can provide a Stripe API key directly or via a parameter:

```csharp
// Direct API key
var stripe = builder.AddStripe("stripe")
    .WithListen("http://localhost:5082/webhooks")
    .WithApiKey("sk_test_...");

// Using a parameter (recommended for sensitive data)
var apiKey = builder.AddParameter("stripe-api-key", secret: true);
var stripe = builder.AddStripe("stripe")
    .WithListen("http://localhost:5082/webhooks")
    .WithApiKey(apiKey);
```

### Filtering events

You can filter which webhook events the Stripe CLI listens for:

```csharp
var stripe = builder.AddStripe("stripe")
    .WithListen("http://localhost:5082/webhooks", 
                events: "payment_intent.created,charge.succeeded");
```

## How it works

The Stripe CLI integration:
- Runs `stripe listen --forward-to <url>` to listen for webhook events from Stripe's test environment
- Forwards those events to your local application endpoint
- Exposes the webhook signing secret so your application can verify webhook authenticity
- Provides a development-friendly way to test webhook integrations without deploying to production

## Additional Information

For more information about the Stripe CLI:
- [Stripe CLI Documentation](https://stripe.com/docs/stripe-cli)
- [Testing webhooks locally](https://stripe.com/docs/webhooks/test)

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-stripe

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire

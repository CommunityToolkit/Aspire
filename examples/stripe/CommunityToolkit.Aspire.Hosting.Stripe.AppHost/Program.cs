var builder = DistributedApplication.CreateBuilder(args);

var stripe = builder.AddStripe("stripe")
    .WithListen("http://localhost:5082/payments/stripe-webhook");

var api = builder.AddProject<Projects.API>("api")
    .WithReference(stripe);

builder.Build().Run();

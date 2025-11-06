var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Stripe Webhook API");

app.MapPost("/payments/stripe-webhook", async (HttpContext context) =>
{
    var webhookSecret = builder.Configuration["STRIPE_WEBHOOK_SECRET"];
    
    using var reader = new StreamReader(context.Request.Body);
    var json = await reader.ReadToEndAsync();
    
    // In a real application, you would verify the webhook signature here
    // using the STRIPE_WEBHOOK_SECRET
    
    return Results.Ok(new { received = true });
});

app.MapGet("/health", () => Results.Ok());

app.Run();

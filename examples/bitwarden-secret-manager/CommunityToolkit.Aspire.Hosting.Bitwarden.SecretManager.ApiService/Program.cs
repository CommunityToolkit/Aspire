using Bitwarden.Sdk;
using CommunityToolkit.Aspire.Bitwarden.SecretManager;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Register the Bitwarden secret manager client with Aspire configuration binding.
// Use the same connection name as the Bitwarden project in the AppHost
// Configuration is injected as Aspire:Bitwarden:SecretManager:{connection_name}:{setting}
// (e.g. Aspire:Bitwarden:SecretManager:secrets:AccessToken).
builder.AddBitwardenSecretManagerClient(connectionName: "secrets", settings =>
{
    // You can optionally override Aspire injected values here or set additional client settings.
    settings.IdentityUrl = "https://vault.bitwarden.com/identity";
    settings.ApiUrl = "https://vault.bitwarden.com/api";
    settings.DisableHealthChecks = true;
});

var app = builder.Build();

app.MapGet("/", ([FromQuery] string? apiKey, BitwardenClient client, BitwardenSecretManagerClientSettings settings, IConfiguration configuration) =>
{
    Guid secretId = configuration.GetValue<Guid>("DEMO_API_KEY_SECRET_ID");
    SecretResponse secret = client.Secrets.Get(secretId);
    if (string.IsNullOrEmpty(apiKey))
    {
        return Results.Problem("Missing apiKey query parameter.", statusCode: StatusCodes.Status401Unauthorized);
    }
    else if (secret.Value != apiKey)
    {
        return Results.Problem("Invalid apiKey.", statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Text("""
        Access granted to protected resource!
        
        But please don't use query parameters for API keys in real applications... this is just a demo!
        Consider using an HTTP header or similar approach to keep secrets out of URLs and logs.
        """);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
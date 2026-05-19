using CommunityToolkit.Aspire.Bitwarden.SecretManager;

var builder = WebApplication.CreateBuilder(args);

builder.AddBitwardenSecretManagerClient("bitwarden", settings => settings.DisableHealthChecks = true);

var app = builder.Build();

app.MapGet("/", (Bitwarden.Sdk.BitwardenClient client, BitwardenSecretManagerClientSettings settings) => Results.Ok(new
{
    client = client.GetType().Name,
    settings.OrganizationId,
    settings.ProjectId,
}));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
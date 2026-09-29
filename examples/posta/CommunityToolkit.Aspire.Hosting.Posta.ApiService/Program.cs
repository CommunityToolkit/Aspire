using CommunityToolkit.Aspire.Posta.Clients;
using CommunityToolkit.Aspire.Posta.Models.Info;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddPostaClient("posta");

WebApplication app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Name = "Posta client example" }));

app.MapGet("/posta/info", async (IPostaClient posta, CancellationToken cancellationToken) =>
{
    ApplicationInfoResponse? response = await posta.Info.ApplicationInfoAsync(cancellationToken: cancellationToken);

    return response is null ? Results.NoContent() : Results.Ok(response);
});

app.MapGet("/health", () => Results.Ok());

app.Run();
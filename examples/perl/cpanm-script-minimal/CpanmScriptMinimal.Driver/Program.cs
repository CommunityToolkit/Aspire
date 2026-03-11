var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "cpanm-script-minimal driver running");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

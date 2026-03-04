var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "cpan-script-minimal driver running");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNeonDataSource("neondb");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "Neon API Service");

app.MapGet("/test", async (NpgsqlDataSource dataSource) =>
{
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT version()";
    var version = await command.ExecuteScalarAsync();
    return new { version, message = "Connected to Neon successfully!" };
});

app.Run();

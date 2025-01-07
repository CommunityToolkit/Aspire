using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

builder.AddSqliteClient("sqlite");

var app = builder.Build();
// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.MapDefaultEndpoints();

await SetupDatabase(app);

app.MapGet("/", () => "Hello World!");

var testGroup = app.MapGroup("/test");

testGroup.MapGet("/", async (SqliteConnection db) =>
    {
        await db.OpenAsync();
        var command = db.CreateCommand();
        command.CommandText = "SELECT name FROM test";
        var result = await command.ExecuteScalarAsync();
        return result?.ToString() ?? "No data";
    });
testGroup.MapPost("/", async (SqliteConnection db, [FromBody] string name) =>
    {
        await db.OpenAsync();
        var command = db.CreateCommand();
        command.CommandText = "INSERT INTO test (name) VALUES ($name)";
        command.Parameters.AddWithValue("$name", name);
        await command.ExecuteNonQueryAsync();
        return Results.Created($"/test/{name}", name);
    });

app.Run();

static async Task SetupDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SqliteConnection>();
    await db.OpenAsync();

    var command = db.CreateCommand();
    command.CommandText = "CREATE TABLE IF NOT EXISTS test (id INTEGER PRIMARY KEY, name TEXT)";
    await command.ExecuteNonQueryAsync();
}
using CommunityToolkit.Aspire.Sqlite.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

builder.AddSqliteClient("sqlite");
builder.AddSqliteDbContext<BloggingContext>("sqlite-ef");

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

var blogGroup = app.MapGroup("/blog");
blogGroup.MapGet("/", async (BloggingContext db) =>
    {
        var blogs = await db.Blogs.ToListAsync();
        return Results.Ok(blogs);
    });
blogGroup.MapPost("/", async (BloggingContext db, [FromBody] Blog blog) =>
    {
        db.Blogs.Add(blog);
        await db.SaveChangesAsync();
        return Results.Created($"/blog/{blog.BlogId}", blog);
    });
blogGroup.MapGet("/{id}", async (BloggingContext db, int id) =>
    {
        var blog = await db.Blogs.FindAsync(id);
        if (blog is null)
        {
            return Results.NotFound();
        }
        return Results.Ok(blog);
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

    using var context = scope.ServiceProvider.GetRequiredService<BloggingContext>();
    await context.Database.MigrateAsync();
}
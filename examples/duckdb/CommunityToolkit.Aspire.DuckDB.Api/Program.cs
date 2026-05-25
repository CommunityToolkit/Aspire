using DuckDB.NET.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();

builder.AddDuckDBConnection("analytics");

var app = builder.Build();
app.UseExceptionHandler();
app.MapDefaultEndpoints();

await SeedDatabase(app);

app.MapGet("/", () => "DuckDB Analytics API");

var analyticsGroup = app.MapGroup("/analytics");

analyticsGroup.MapGet("/summary", async (DuckDBConnection db) =>
{
    await db.OpenAsync();
    using var command = db.CreateCommand();
    command.CommandText = """
        SELECT 
            category,
            COUNT(*) as total_orders,
            CAST(SUM(amount) AS DOUBLE) as total_revenue,
            CAST(AVG(amount) AS DOUBLE) as avg_order_value
        FROM orders
        GROUP BY category
        ORDER BY total_revenue DESC
        """;
    using var reader = await command.ExecuteReaderAsync();

    var results = new List<object>();
    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            Category = reader.GetString(0),
            TotalOrders = reader.GetInt64(1),
            TotalRevenue = reader.GetDouble(2),
            AvgOrderValue = reader.GetDouble(3)
        });
    }
    return Results.Ok(results);
});

analyticsGroup.MapPost("/orders", async (DuckDBConnection db) =>
{
    await db.OpenAsync();
    using var command = db.CreateCommand();
    command.CommandText = """
        INSERT INTO orders (category, product, amount, order_date) VALUES
        ('Electronics', 'Laptop', 999.99, CURRENT_TIMESTAMP)
        """;
    await command.ExecuteNonQueryAsync();
    return Results.Created("/analytics/summary", null);
});

analyticsGroup.MapGet("/orders", async (DuckDBConnection db) =>
{
    await db.OpenAsync();
    using var command = db.CreateCommand();
    command.CommandText = "SELECT id, category, product, amount, order_date FROM orders ORDER BY order_date DESC LIMIT 100";
    using var reader = await command.ExecuteReaderAsync();

    var results = new List<object>();
    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            Id = reader.GetInt32(0),
            Category = reader.GetString(1),
            Product = reader.GetString(2),
            Amount = reader.GetDouble(3),
            OrderDate = reader.GetDateTime(4)
        });
    }
    return Results.Ok(results);
});

app.Run();

static async Task SeedDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DuckDBConnection>();
    await db.OpenAsync();

    using var createCommand = db.CreateCommand();
    createCommand.CommandText = """
        CREATE SEQUENCE IF NOT EXISTS orders_id_seq START 1;
        CREATE TABLE IF NOT EXISTS orders (
            id INTEGER DEFAULT nextval('orders_id_seq'),
            category VARCHAR,
            product VARCHAR,
            amount DOUBLE,
            order_date TIMESTAMP
        )
        """;
    await createCommand.ExecuteNonQueryAsync();

    // Check if data already exists
    using var countCommand = db.CreateCommand();
    countCommand.CommandText = "SELECT COUNT(*) FROM orders";
    var count = (long)(await countCommand.ExecuteScalarAsync())!;

    if (count == 0)
    {
        using var seedCommand = db.CreateCommand();
        seedCommand.CommandText = """
            INSERT INTO orders (id, category, product, amount, order_date) VALUES
                (1, 'Electronics', 'Laptop', 1299.99, '2025-01-15 10:30:00'),
                (2, 'Electronics', 'Phone', 899.99, '2025-01-16 14:20:00'),
                (3, 'Books', 'Programming Guide', 49.99, '2025-01-17 09:15:00'),
                (4, 'Books', 'Data Science Manual', 59.99, '2025-01-18 11:45:00'),
                (5, 'Clothing', 'T-Shirt', 29.99, '2025-01-19 16:00:00')
            """;
        await seedCommand.ExecuteNonQueryAsync();
    }
}

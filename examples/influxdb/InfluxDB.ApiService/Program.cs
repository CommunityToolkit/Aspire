using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfluxDBClient(connectionName: "influxdb");

var app = builder.Build();

app.MapGet("/write", (InfluxDBClient client) =>
{
    var org = "default";
    var bucket = "default";

    // Create some sample data points
    var point1 = PointData
        .Measurement("temperature")
        .Tag("location", "server-room")
        .Field("value", 22.5)
        .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

    var point2 = PointData
        .Measurement("temperature")
        .Tag("location", "office")
        .Field("value", 21.0)
        .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

    var point3 = PointData
        .Measurement("humidity")
        .Tag("location", "server-room")
        .Field("value", 45.2)
        .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

    // Write data to InfluxDB
    using var writeApi = client.GetWriteApi();
    writeApi.WritePoint(point1, bucket, org);
    writeApi.WritePoint(point2, bucket, org);
    writeApi.WritePoint(point3, bucket, org);

    return Results.Ok(new { message = "Data written successfully", points = 3 });
});

app.MapGet("/read", async (InfluxDBClient client) =>
{
    var org = "default";
    var bucket = "default";

    // Query all data from the last hour
    var query = $@"
        from(bucket: ""{bucket}"")
            |> range(start: -1h)
            |> filter(fn: (r) => r._measurement == ""temperature"" or r._measurement == ""humidity"")
    ";

    var queryApi = client.GetQueryApi();
    var tables = await queryApi.QueryAsync(query, org);

    var results = new List<object>();

    foreach (var table in tables)
    {
        foreach (var record in table.Records)
        {
            results.Add(new
            {
                time = record.GetTime(),
                measurement = record.GetMeasurement(),
                location = record.GetValueByKey("location"),
                field = record.GetField(),
                value = record.GetValue()
            });
        }
    }

    return Results.Ok(new { count = results.Count, data = results });
});

app.MapDefaultEndpoints();

app.Run();

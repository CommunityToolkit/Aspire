using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/trace", () => TypedResults.Ok(new TraceResponse(Activity.Current?.TraceId.ToString())))
    .WithName("CreateTrace")
    .WithSummary("Returns the trace identifier for this request.");

app.Run();

/// <summary>
/// Represents the trace identifier of an API request.
/// </summary>
public sealed record TraceResponse(string? TraceId);
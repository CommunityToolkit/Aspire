using Amazon.S3;
using Amazon.S3.Model;
using CommunityToolkit.Aspire.SeaweedFS.Client;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, HealthChecks, etc.)
builder.AddServiceDefaults();

// Add essential web services
builder.Services.AddProblemDetails();

// Register SeaweedFS client integrations (S3 and Filer)
builder.AddSeaweedFSS3Client("seaweedfs");
builder.AddSeaweedFSFilerClient("seaweedfs");

var app = builder.Build();

app.UseExceptionHandler();

// ==========================================
// 🌊 S3 ENDPOINTS (AWS Compatibility)
// ==========================================
var s3Group = app.MapGroup("/s3").WithTags("S3 API");

s3Group.MapPost("/buckets", async ([FromQuery] string bucketName, IAmazonS3 s3Client) =>
{
    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
    return Results.Ok(new { Message = $"Bucket '{bucketName}' successfully created via S3 API." });
});

s3Group.MapPost("/upload", async ([FromQuery] string bucketName, [FromQuery] string key, [FromBody] string content, IAmazonS3 s3Client) =>
{
    await s3Client.PutObjectAsync(new PutObjectRequest
    {
        BucketName = bucketName,
        Key = key,
        ContentBody = content
    });
    return Results.Ok(new { Message = $"File '{key}' successfully uploaded to bucket '{bucketName}'." });
});

s3Group.MapGet("/download", async ([FromQuery] string bucketName, [FromQuery] string key, IAmazonS3 s3Client) =>
{
    var response = await s3Client.GetObjectAsync(bucketName, key);
    using var reader = new StreamReader(response.ResponseStream);
    var content = await reader.ReadToEndAsync();

    return Results.Ok(new { Bucket = bucketName, Key = key, Content = content });
});

// ==========================================
// 📁 FILER ENDPOINTS (Native API)
// ==========================================
var filerGroup = app.MapGroup("/filer").WithTags("Filer API");

filerGroup.MapGet("/list", async (SeaweedFSFilerClient filerClient) =>
{
    // Requesting 'application/json' ensures the Filer returns a JSON directory listing instead of HTML
    var request = new HttpRequestMessage(HttpMethod.Get, "/");
    request.Headers.Add("Accept", "application/json");

    var response = await filerClient.HttpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

filerGroup.MapPost("/upload", async ([FromQuery] string fileName, [FromBody] string content, SeaweedFSFilerClient filerClient) =>
{
    var stringContent = new StringContent(content);

    // Uploads the file directly to the root of the Filer
    var response = await filerClient.HttpClient.PutAsync($"/{fileName.TrimStart('/')}", stringContent);
    response.EnsureSuccessStatusCode();

    return Results.Ok(new { Message = $"File '{fileName}' successfully uploaded via native Filer API." });
});

app.MapDefaultEndpoints();

app.Run();
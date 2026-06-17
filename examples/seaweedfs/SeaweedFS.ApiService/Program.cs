using Amazon.S3;
using Amazon.S3.Model;
using CommunityToolkit.Aspire.SeaweedFS.Client;
using Microsoft.AspNetCore.Mvc;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add service defaults (OpenTelemetry, HealthChecks, etc.)
builder.AddServiceDefaults();

// Add essential web services
builder.Services.AddProblemDetails();

// =========================================================================
// 🌊 REGISTER SEAWEEDFS CLIENTS
// =========================================================================
// Basic Setup (Endpoints and credentials are automatically injected by AppHost)
builder.AddSeaweedFSS3Client("seaweedfs");
builder.AddSeaweedFSFilerClient("seaweedfs");

/* // -------------------------------------------------------------------------
// ADVANCED SETUP EXAMPLES
// Uncomment and use the blocks below to override client settings programmatically.
// -------------------------------------------------------------------------

builder.AddSeaweedFSS3Client("seaweedfs", settings =>
{
    // The Endpoint is injected automatically, but you can override protocol rules:
    settings.UseSsl = false; 
    settings.ForcePathStyle = true; // Required by SeaweedFS architecture
    
    // Deep configuration of the underlying AWS SDK:
    settings.ConfigureS3Config = s3Config =>
    {
        s3Config.Timeout = TimeSpan.FromSeconds(30);
        s3Config.MaxErrorRetry = 3;
    };
});

builder.AddSeaweedFSFilerClient("seaweedfs", settings =>
{
    // Example of disabling health checks for the Filer specifically
    settings.DisableHealthChecks = true;
});
*/

WebApplication app = builder.Build();

app.UseExceptionHandler();

// ==========================================
// 🌊 S3 ENDPOINTS (AWS Compatibility)
// ==========================================
RouteGroupBuilder s3Group = app.MapGroup("/s3").WithTags("S3 API");

s3Group.MapPost("/buckets", async ([FromQuery] string bucketName, IAmazonS3 s3Client) =>
{
    await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName });
    return Results.Ok(new { Message = "Bucket successfully created via S3 API." });
});

s3Group.MapPost("/upload", async ([FromQuery] string bucketName, [FromQuery] string key, [FromBody] string content, IAmazonS3 s3Client) =>
{
    await s3Client.PutObjectAsync(new PutObjectRequest
    {
        BucketName = bucketName,
        Key = key,
        ContentBody = content
    });
    return Results.Ok(new { Message = "File successfully uploaded to bucket via S3 API." });
});

s3Group.MapGet("/download", async ([FromQuery] string bucketName, [FromQuery] string key, IAmazonS3 s3Client) =>
{
    GetObjectResponse response = await s3Client.GetObjectAsync(bucketName, key);
    using StreamReader reader = new(response.ResponseStream);
    string content = await reader.ReadToEndAsync();

    return Results.Ok(new
    {
        Bucket = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(bucketName),
        Key = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(key),
        Content = content
    });
});

// ==========================================
// 📁 FILER ENDPOINTS (Native API)
// ==========================================
RouteGroupBuilder filerGroup = app.MapGroup("/filer").WithTags("Filer API");

filerGroup.MapGet("/list", async (SeaweedFSFilerClient filerClient) =>
{
    HttpRequestMessage request = new(HttpMethod.Get, "/");
    request.Headers.Add("Accept", "application/json");

    HttpResponseMessage response = await filerClient.HttpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();

    string content = await response.Content.ReadAsStringAsync();
    return Results.Content(content, "application/json");
});

filerGroup.MapPost("/upload", async ([FromQuery] string fileName, [FromBody] string content, SeaweedFSFilerClient filerClient) =>
{
    StringContent stringContent = new(content, System.Text.Encoding.UTF8, "text/plain");

    string safeFileName = Uri.EscapeDataString(fileName.TrimStart('/'));

    HttpResponseMessage response = await filerClient.HttpClient.PutAsync($"/{safeFileName}", stringContent);

    response.EnsureSuccessStatusCode();

    return Results.Ok(new { Message = "File successfully uploaded via native Filer API." });
});

app.MapDefaultEndpoints();

app.Run();
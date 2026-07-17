using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Read AWS config from env vars injected by Aspire's WithReference(floci).
// RegionEndpoint is intentionally omitted — the SDK resolves it from AWS_DEFAULT_REGION
// automatically, and combining ServiceURL + RegionEndpoint in SDK v4 triggers a NPE in
// the endpoint rule engine.
var endpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? "http://localhost:4566";
var region = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-east-1";
var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";

var s3 = new AmazonS3Client(
    new BasicAWSCredentials(accessKey, secretKey),
    new AmazonS3Config
    {
        ServiceURL = endpointUrl,
        ForcePathStyle = true
    });

builder.Services.AddSingleton<IAmazonS3>(s3);

// Creates the demo bucket once Floci is reachable; logs a warning if Floci is still starting.
builder.Services.AddHostedService<BucketInitializer>();

builder.Services.AddHealthChecks()
    .AddAsyncCheck("floci-s3", async ct =>
    {
        try
        {
            var response = await s3.ListBucketsAsync(ct);
            if (response?.Buckets == null)
            {
                return HealthCheckResult.Unhealthy("Floci S3 returned null buckets list");
            }
            return HealthCheckResult.Healthy($"Floci S3 reachable — {response.Buckets.Count} bucket(s)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Floci S3 unreachable", ex);
        }
    });

var app = builder.Build();

app.Logger.LogInformation(
    "Floci endpoint={Url} region={Region} accessKey={Key}",
    endpointUrl, region, accessKey);

// /alive — liveness probe (process is up, no dependency checks)
// /health — readiness probe (checks S3 connectivity)
app.MapGet("/alive", () => Results.Ok());
app.MapHealthChecks("/health");

// --- S3 demo endpoints ---

// List all buckets currently in Floci
app.MapGet("/s3/buckets", async (IAmazonS3 s3) =>
{
    var response = await s3.ListBucketsAsync();
    return (response?.Buckets ?? []).Select(b => new { b.BucketName, b.CreationDate });
});

// Create a new bucket
app.MapPost("/s3/{bucket}", async (string bucket, IAmazonS3 s3) =>
{
    await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
    return Results.Created($"/s3/{bucket}", new { bucket });
});

// Store a text value at bucket/key
app.MapPut("/s3/{bucket}/{*key}", async (string bucket, string key, HttpRequest request, IAmazonS3 s3) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, ContentBody = body });
    return Results.Created($"/s3/{bucket}/{key}", new { bucket, key, size = body.Length });
});

// Retrieve a value previously stored at bucket/key
app.MapGet("/s3/{bucket}/{*key}", async (string bucket, string key, IAmazonS3 s3) =>
{
    try
    {
        var response = await s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key });
        using var reader = new StreamReader(response.ResponseStream);
        return Results.Ok(await reader.ReadToEndAsync());
    }
    catch (AmazonS3Exception ex) when (ex.ErrorCode is "NoSuchKey" or "NoSuchBucket")
    {
        return Results.NotFound(new { bucket, key });
    }
});

app.Run();

/// Hosted service that creates the demo bucket once the app starts.
/// Runs concurrently with the health check; failures are non-fatal since
/// Floci may still be initialising when the API first comes up.
class BucketInitializer(IAmazonS3 s3, ILogger<BucketInitializer> logger) : IHostedService
{
    private const string DemoBucket = "aspire-demo";

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var buckets = await s3.ListBucketsAsync(ct);
            if (buckets?.Buckets == null || buckets.Buckets.Count == 0)
            {
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = DemoBucket }, ct);
                logger.LogInformation("Created demo S3 bucket '{Bucket}'", DemoBucket);
            }
            else if (!buckets.Buckets.Any(b => b.BucketName == DemoBucket))
            {
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = DemoBucket }, ct);
                logger.LogInformation("Created demo S3 bucket '{Bucket}'", DemoBucket);
            }
            else
            {
                logger.LogInformation("Demo S3 bucket '{Bucket}' already exists", DemoBucket);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Demo bucket init deferred — Floci may still be starting");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

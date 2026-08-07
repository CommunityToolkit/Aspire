using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Azure;
using Azure.Storage.Blobs;
using Google.Api.Gax;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// --- AWS (S3) --------------------------------------------------------------
// Read AWS config from env vars injected by Aspire's WithReference(floci).
// RegionEndpoint is intentionally omitted — the SDK resolves it from AWS_DEFAULT_REGION
// automatically, and combining ServiceURL + RegionEndpoint in SDK v4 triggers a NPE in
// the endpoint rule engine.
var awsEndpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL")!;
var awsRegion = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION")!;
var awsAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")!;
var awsSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")!;

var s3 = new AmazonS3Client(
    new BasicAWSCredentials(awsAccessKey, awsSecretKey),
    new AmazonS3Config
    {
        ServiceURL = awsEndpointUrl,
        ForcePathStyle = true
    });

builder.Services.AddSingleton<IAmazonS3>(s3);

// --- Azure (Blob Storage) ----------------------------------------------------
// AZURE_STORAGE_CONNECTION_STRING is injected by Aspire's WithReference(flociAzure) —
// it already points BlobEndpoint at the Floci Azure emulator with the well-known
// devstoreaccount1 dev credentials.
var azureConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")!;

var blobServiceClient = new BlobServiceClient(azureConnectionString);

builder.Services.AddSingleton(blobServiceClient);

// --- GCP (Cloud Storage) ------------------------------------------------------
// STORAGE_EMULATOR_HOST / GOOGLE_CLOUD_PROJECT are injected by Aspire's WithReference(flociGcp).
// EmulatorDetection.EmulatorOnly makes the client library read STORAGE_EMULATOR_HOST and skip
// real GCP credential resolution entirely.
var gcpProjectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")!;

var gcpStorageClient = await new StorageClientBuilder
{
    EmulatorDetection = EmulatorDetection.EmulatorOnly
}.BuildAsync();

builder.Services.AddSingleton(gcpStorageClient);

// Creates the demo bucket/container once each Floci emulator is reachable; logs a warning if
// still starting. Runs concurrently with the health checks — failures here are non-fatal.
builder.Services.AddHostedService<AwsBucketInitializer>();
builder.Services.AddHostedService<AzureContainerInitializer>();
builder.Services.AddHostedService<GcpBucketInitializer>();

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
    })
    .AddAsyncCheck("floci-azure-blob", async ct =>
    {
        try
        {
            var count = 0;
            await foreach (var _ in blobServiceClient.GetBlobContainersAsync(cancellationToken: ct))
            {
                count++;
            }
            return HealthCheckResult.Healthy($"Floci Azure Blob reachable — {count} container(s)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Floci Azure Blob unreachable", ex);
        }
    })
    .AddAsyncCheck("floci-gcp-storage", async ct =>
    {
        try
        {
            var count = 0;
            await foreach (var _ in gcpStorageClient.ListBucketsAsync(gcpProjectId).WithCancellation(ct))
            {
                count++;
            }
            return HealthCheckResult.Healthy($"Floci GCP Storage reachable — {count} bucket(s)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Floci GCP Storage unreachable", ex);
        }
    });

var app = builder.Build();

app.Logger.LogInformation(
    "AWS endpoint={AwsEndpoint} region={AwsRegion} | Azure blob endpoint present={HasAzureConn} | GCP project={GcpProject}",
    awsEndpointUrl, awsRegion, !string.IsNullOrEmpty(azureConnectionString), gcpProjectId);

// /alive — liveness probe (process is up, no dependency checks)
// /health — readiness probe (checks S3/Blob/GCS connectivity)
app.MapGet("/alive", () => Results.Ok());
app.MapHealthChecks("/health");

// --- S3 demo endpoints -------------------------------------------------------

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

// --- Azure Blob demo endpoints ------------------------------------------------

// List all containers currently in Floci Azure
app.MapGet("/azure/containers", async (BlobServiceClient blobServiceClient) =>
{
    var containers = new List<object>();
    await foreach (var container in blobServiceClient.GetBlobContainersAsync())
    {
        containers.Add(new { container.Name, LastModified = container.Properties.LastModified });
    }
    return containers;
});

// Create a new container
app.MapPost("/azure/{container}", async (string container, BlobServiceClient blobServiceClient) =>
{
    await blobServiceClient.GetBlobContainerClient(container).CreateIfNotExistsAsync();
    return Results.Created($"/azure/{container}", new { container });
});

// Store a text value at container/key as a blob
app.MapPut("/azure/{container}/{*key}", async (string container, string key, HttpRequest request, BlobServiceClient blobServiceClient) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    var containerClient = blobServiceClient.GetBlobContainerClient(container);
    await containerClient.CreateIfNotExistsAsync();
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
    await containerClient.GetBlobClient(key).UploadAsync(stream, overwrite: true);
    return Results.Created($"/azure/{container}/{key}", new { container, key, size = body.Length });
});

// Retrieve a value previously stored at container/key
app.MapGet("/azure/{container}/{*key}", async (string container, string key, BlobServiceClient blobServiceClient) =>
{
    try
    {
        var blobClient = blobServiceClient.GetBlobContainerClient(container).GetBlobClient(key);
        var response = await blobClient.DownloadContentAsync();
        return Results.Ok(response.Value.Content.ToString());
    }
    catch (RequestFailedException ex) when (ex.ErrorCode is "BlobNotFound" or "ContainerNotFound")
    {
        return Results.NotFound(new { container, key });
    }
});

// --- GCP Storage demo endpoints ------------------------------------------------

// List all buckets currently in Floci GCP
app.MapGet("/gcp/buckets", async (StorageClient gcpStorageClient) =>
{
    var buckets = new List<object>();
    await foreach (var bucket in gcpStorageClient.ListBucketsAsync(gcpProjectId))
    {
        buckets.Add(new { bucket.Name, TimeCreated = bucket.TimeCreatedDateTimeOffset });
    }
    return buckets;
});

// Create a new bucket
app.MapPost("/gcp/{bucket}", async (string bucket, StorageClient gcpStorageClient) =>
{
    await gcpStorageClient.CreateBucketAsync(gcpProjectId, bucket);
    return Results.Created($"/gcp/{bucket}", new { bucket });
});

// Store a text value at bucket/key as an object
app.MapPut("/gcp/{bucket}/{*key}", async (string bucket, string key, HttpRequest request, StorageClient gcpStorageClient) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
    await gcpStorageClient.UploadObjectAsync(bucket, key, "text/plain", stream);
    return Results.Created($"/gcp/{bucket}/{key}", new { bucket, key, size = body.Length });
});

// Retrieve a value previously stored at bucket/key
app.MapGet("/gcp/{bucket}/{*key}", async (string bucket, string key, StorageClient gcpStorageClient) =>
{
    try
    {
        using var stream = new MemoryStream();
        await gcpStorageClient.DownloadObjectAsync(bucket, key, stream);
        return Results.Ok(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }
    catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.NotFound(new { bucket, key });
    }
});

app.Run();

/// Hosted service that creates the demo bucket once the app starts.
/// Runs concurrently with the health check; failures are non-fatal since
/// Floci may still be initialising when the API first comes up.
class AwsBucketInitializer(IAmazonS3 s3, ILogger<AwsBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var buckets = await s3.ListBucketsAsync(ct);
            if (buckets?.Buckets == null || !buckets.Buckets.Any(b => b.BucketName == Demo.Name))
            {
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = Demo.Name }, ct);
                logger.LogInformation("Created demo S3 bucket '{Bucket}'", Demo.Name);
            }
            else
            {
                logger.LogInformation("Demo S3 bucket '{Bucket}' already exists", Demo.Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Demo S3 bucket init deferred — Floci may still be starting");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

/// Hosted service that creates the demo blob container once the app starts.
/// Runs concurrently with the health check; failures are non-fatal since
/// Floci may still be initialising when the API first comes up.
class AzureContainerInitializer(BlobServiceClient blobServiceClient, ILogger<AzureContainerInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var response = await blobServiceClient.GetBlobContainerClient(Demo.Name).CreateIfNotExistsAsync(cancellationToken: ct);
            logger.LogInformation(
                response != null ? "Created demo Azure Blob container '{Container}'" : "Demo Azure Blob container '{Container}' already exists",
                Demo.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Demo Azure Blob container init deferred — Floci may still be starting");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

/// Hosted service that creates the demo bucket once the app starts.
/// Runs concurrently with the health check; failures are non-fatal since
/// Floci may still be initialising when the API first comes up.
class GcpBucketInitializer(StorageClient gcpStorageClient, ILogger<GcpBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "floci-local";
            var exists = false;
            await foreach (var bucket in gcpStorageClient.ListBucketsAsync(projectId).WithCancellation(ct))
            {
                if (bucket.Name == Demo.Name)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                await gcpStorageClient.CreateBucketAsync(projectId, Demo.Name, cancellationToken: ct);
                logger.LogInformation("Created demo GCP Storage bucket '{Bucket}'", Demo.Name);
            }
            else
            {
                logger.LogInformation("Demo GCP Storage bucket '{Bucket}' already exists", Demo.Name);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Demo GCP Storage bucket init deferred — Floci may still be starting");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

/// Shared demo bucket/container name used across all three clouds.
static class Demo
{
    public const string Name = "aspire-demo";
}

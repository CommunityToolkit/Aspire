using CommunityToolkit.Aspire.Minio.Client;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMinioClient("minio");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "Hello World!");

app.MapPut("/buckets/{bucketName}", async (string bucketName, [FromServices] IMinioClient minioClient) =>
{
    var mbArgs = new MakeBucketArgs()
        .WithBucket(bucketName);
    await minioClient.MakeBucketAsync(mbArgs);
    
    return Results.Ok();
});

app.MapGet("/buckets/{bucketName}", async (string bucketName, [FromServices] IMinioClient minioClient) =>
{
     var exists = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
     
     if(exists)
         return Results.Ok();
     return Results.NotFound();
});

app.Run();
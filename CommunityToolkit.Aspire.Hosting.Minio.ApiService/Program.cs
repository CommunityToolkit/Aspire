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

app.MapGet("/search/{bucketId}", async (string bucketId, [FromServices] MinioClient minioClient) =>
{
    return await minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
        .WithBucket(bucketId));
});

app.Run();
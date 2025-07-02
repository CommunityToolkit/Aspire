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

app.MapPost("/buckets/{bucketName}/{fileName}/upload",
    async ([FromRoute] string bucketName,
        [FromRoute] string fileName,
        HttpRequest request,
        [FromServices] IMinioClient minioClient) =>
    {
        var memstream = new MemoryStream();
        
        await request.Body.CopyToAsync(memstream);
        
        var length = memstream.Length;
        memstream.Seek(0, SeekOrigin.Begin);
        
        var putObjectArgs = new PutObjectArgs()
            .WithObject(fileName)
            .WithBucket(bucketName)
            .WithStreamData(memstream)
            .WithObjectSize(length);

        await minioClient.PutObjectAsync(putObjectArgs);

        return Results.Ok();
    }).DisableAntiforgery();

app.MapGet("/buckets/{bucketName}/{fileName}/download",
    async (string bucketName, string fileName, [FromServices] IMinioClient minioClient) =>
    {
        var memStream = new MemoryStream();
        
        var getObjectArgs = new GetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(fileName)
            .WithCallbackStream(stream =>
            {
                stream.CopyToAsync(memStream);
            });
        
        await minioClient.GetObjectAsync(getObjectArgs);
        
        return Results.File(memStream.ToArray(), "application/octet-stream", fileName);
    });

app.Run();
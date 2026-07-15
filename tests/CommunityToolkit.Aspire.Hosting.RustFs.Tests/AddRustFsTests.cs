// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.RustFs.Tests;

public class AddRustFsTests
{
    [Fact]
    public void RustFsResourceGetsAdded()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRustFs("rustfs");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustFsResource>());

        Assert.Equal("rustfs", resource.Name);
    }

    [Fact]
    public void RustFsResourceHasHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRustFs("rustfs");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<RustFsResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("rustfs", resource.Name);

        var result = resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations);

        Assert.True(result);
        Assert.NotNull(annotations);

        Assert.Single(annotations);
    }

    [Fact]
    public void RustFsResourceHasCorrectEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRustFs("rustfs");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustFsResource>());

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToList();

        Assert.Equal(2, endpoints.Count);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(9000, primaryEndpoint.TargetPort);

        var consoleEndpoint = Assert.Single(endpoints, e => e.Name == "console");
        Assert.Equal(9001, consoleEndpoint.TargetPort);
    }

    [Fact]
    public async Task RustFsResourceConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();

        var accessKey = builder.AddParameter("accessKey", "testaccesskey");
        var secretKey = builder.AddParameter("secretKey", "testsecretkey");

        var rustfs = builder.AddRustFs("rustfs", accessKey, secretKey)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 2000));

        var connectionString = await rustfs.Resource.GetConnectionStringAsync();

        Assert.Equal("Endpoint=http://localhost:2000;AccessKey=testaccesskey;SecretKey=testsecretkey", connectionString);
    }

    [Fact]
    public void RustFsResourceWithCustomPort()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRustFs("rustfs", port: 3000, consolePort: 3001);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<RustFsResource>());

        var primaryEndpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), e => e.Name == "http");
        Assert.Equal(3000, primaryEndpoint.Port);

        var consoleEndpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), e => e.Name == "console");
        Assert.Equal(3001, consoleEndpoint.Port);
    }

    [Fact]
    public void AddBucketRegistersChildBucketResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rustfs = builder.AddRustFs("rustfs");
        var bucketBuilder = rustfs.AddBucket("mybucket");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var bucket = Assert.Single(appModel.Resources.OfType<RustFsBucketResource>());

        Assert.Equal("mybucket", bucket.BucketName);
        Assert.Same(rustfs.Resource, bucket.Parent);
        Assert.Same(bucket, bucketBuilder.Resource);

        var parentAnnotation = bucket.Annotations.OfType<ResourceRelationshipAnnotation>()
            .SingleOrDefault(a => a.Type == "Parent");
        Assert.NotNull(parentAnnotation);
        Assert.Same(rustfs.Resource, parentAnnotation.Resource);
    }

    [Fact]
    public void AddBucketDoesNotAddMcContainer()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rustfs = builder.AddRustFs("rustfs");
        rustfs.AddBucket("mybucket");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var mcContainers = appModel.Resources
            .OfType<ContainerResource>()
            .Where(r => r.Annotations.OfType<ContainerImageAnnotation>().Any(a => a.Image == "minio/mc"))
            .ToList();

        Assert.Empty(mcContainers);
    }

    [Fact]
    public void AddBucketWithDotInNameUsesValidResourceName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rustfs = builder.AddRustFs("rustfs");
        rustfs.AddBucket("my.bucket");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var bucket = Assert.Single(appModel.Resources.OfType<RustFsBucketResource>());

        Assert.DoesNotContain(".", bucket.Name);
        Assert.Equal("my.bucket", bucket.BucketName);
    }

    [Fact]
    public void AddBucketListRegistersOneResourcePerBucket()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rustfs = builder.AddRustFs("rustfs");
        rustfs.AddBucket(["bucket1", "bucket2"]);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var buckets = appModel.Resources.OfType<RustFsBucketResource>().ToList();
        Assert.Equal(2, buckets.Count);
        Assert.Contains(buckets, b => b.BucketName == "bucket1");
        Assert.Contains(buckets, b => b.BucketName == "bucket2");
    }

    [Fact]
    public void AddBucketListSkipsWhitespaceEntries()
    {
        var builder = DistributedApplication.CreateBuilder();

        var rustfs = builder.AddRustFs("rustfs");
        rustfs.AddBucket(["bucket1", "   ", "bucket2"]);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var buckets = appModel.Resources.OfType<RustFsBucketResource>().ToList();
        Assert.Equal(2, buckets.Count);
    }

    [Fact]
    public async Task BucketResourceConnectionStringIncludesBucketName()
    {
        var builder = DistributedApplication.CreateBuilder();

        var accessKey = builder.AddParameter("accessKey", "ak");
        var secretKey = builder.AddParameter("secretKey", "sk");

        var rustfs = builder.AddRustFs("rustfs", accessKey, secretKey)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 9000));

        var bucket = rustfs.AddBucket("data");

        var connectionString = await bucket.Resource.ConnectionStringExpression.GetValueAsync(default);

        Assert.Equal("Endpoint=http://localhost:9000;AccessKey=ak;SecretKey=sk;Bucket=data", connectionString);
    }
}

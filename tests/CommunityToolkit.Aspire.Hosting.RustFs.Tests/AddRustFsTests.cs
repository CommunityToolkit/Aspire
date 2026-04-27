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
}

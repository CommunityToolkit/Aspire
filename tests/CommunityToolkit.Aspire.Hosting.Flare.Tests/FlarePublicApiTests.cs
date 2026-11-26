// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;

namespace CommunityToolkit.Aspire.Hosting.Flare.Tests;

public class FlarePublicApiTests
{
    [Fact]
    public void AddFlareShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        var appBuilder = TestDistributedApplicationBuilder.Create();
        var clickhouse = appBuilder.AddClickHouse("clickhouse");
        var redis = appBuilder.AddRedis("redis");

        var action = () => builder.AddFlare("flare", clickhouse, redis);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddFlareShouldThrowWhenNameIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var clickhouse = builder.AddClickHouse("clickhouse");
        var redis = builder.AddRedis("redis");
        string name = null!;

        var action = () => builder.AddFlare(name, clickhouse, redis);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddFlareShouldThrowWhenClickHouseIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var redis = builder.AddRedis("redis");
        IResourceBuilder<ClickHouseServerResource> clickhouse = null!;

        var action = () => builder.AddFlare("flare", clickhouse, redis);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(clickhouse), exception.ParamName);
    }

    [Fact]
    public void AddFlareShouldThrowWhenRedisIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var clickhouse = builder.AddClickHouse("clickhouse");
        IResourceBuilder<RedisResource> redis = null!;

        var action = () => builder.AddFlare("flare", clickhouse, redis);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(redis), exception.ParamName);
    }

    [Fact]
    public void WithDataVolumeShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<FlareResource> builder = null!;

        var action = () => builder.WithDataVolume();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithIdentityDatabaseShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<FlareResource> builder = null!;
        var database = TestDistributedApplicationBuilder.Create().AddConnectionString("flare-identity");

        var action = () => builder.WithIdentityDatabase(database);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithIdentityDatabaseShouldThrowWhenDatabaseIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var flare = builder.AddFlare("flare", builder.AddClickHouse("clickhouse"), builder.AddRedis("redis"));
        IResourceBuilder<IResourceWithConnectionString> database = null!;

        var action = () => flare.WithIdentityDatabase(database);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(database), exception.ParamName);
    }

    [Fact]
    public void WithColdStorageShouldThrowWhenBuilderIsNull()
    {
        var appBuilder = TestDistributedApplicationBuilder.Create();
        IResourceBuilder<FlareResource> builder = null!;

        var action = () => builder.WithColdStorage(
            ReferenceExpression.Create($"http://rustfs:9000/flare-cold/"),
            appBuilder.AddParameter("access-key"),
            appBuilder.AddParameter("secret-key"));

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithColdStorageShouldThrowWhenEndpointIsNull()
    {
        var appBuilder = TestDistributedApplicationBuilder.Create();
        var flare = appBuilder.AddFlare("flare", appBuilder.AddClickHouse("clickhouse"), appBuilder.AddRedis("redis"));
        ReferenceExpression endpoint = null!;

        var action = () => flare.WithColdStorage(endpoint, appBuilder.AddParameter("access-key"), appBuilder.AddParameter("secret-key"));

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(endpoint), exception.ParamName);
    }

    [Fact]
    public void WithColdStorageShouldThrowWhenAccessKeyIsNull()
    {
        var appBuilder = TestDistributedApplicationBuilder.Create();
        var flare = appBuilder.AddFlare("flare", appBuilder.AddClickHouse("clickhouse"), appBuilder.AddRedis("redis"));
        IResourceBuilder<ParameterResource> accessKey = null!;

        var action = () => flare.WithColdStorage(
            ReferenceExpression.Create($"http://rustfs:9000/flare-cold/"), accessKey, appBuilder.AddParameter("secret-key"));

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(accessKey), exception.ParamName);
    }

    [Fact]
    public void WithColdStorageShouldThrowWhenSecretKeyIsNull()
    {
        var appBuilder = TestDistributedApplicationBuilder.Create();
        var flare = appBuilder.AddFlare("flare", appBuilder.AddClickHouse("clickhouse"), appBuilder.AddRedis("redis"));
        IResourceBuilder<ParameterResource> secretKey = null!;

        var action = () => flare.WithColdStorage(
            ReferenceExpression.Create($"http://rustfs:9000/flare-cold/"), appBuilder.AddParameter("access-key"), secretKey);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(secretKey), exception.ParamName);
    }

    [Fact]
    public void WithIngestPortsShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<FlareResource> builder = null!;

        var action = () => builder.WithIngestPorts(4317, 4318);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithApiKeyShouldThrowWhenApiKeyIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var flare = builder.AddFlare("flare", builder.AddClickHouse("clickhouse"), builder.AddRedis("redis"));
        IResourceBuilder<ParameterResource> apiKey = null!;

        var action = () => flare.WithApiKey(apiKey);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(apiKey), exception.ParamName);
    }

    [Fact]
    public void WithFlareImageTagShouldThrowWhenTagIsEmpty()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var flare = builder.AddFlare("flare", builder.AddClickHouse("clickhouse"), builder.AddRedis("redis"));

        var action = () => flare.WithFlareImageTag("");

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("tag", exception.ParamName);
    }

    [Fact]
    public void WithOtlpExporterShouldThrowWhenFlareIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var container = builder.AddContainer("app", "image");
        IResourceBuilder<FlareResource> flare = null!;

        var action = () => container.WithOtlpExporter(flare);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(flare), exception.ParamName);
    }

    [Fact]
    public void WithOtlpExporterShouldThrowWhenProtocolIsUndefined()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var container = builder.AddContainer("app", "image");
        var flare = builder.AddFlare("flare", builder.AddClickHouse("clickhouse"), builder.AddRedis("redis"));

        var action = () => container.WithOtlpExporter(flare, (OtlpProtocol)42);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal("protocol", exception.ParamName);
    }
}

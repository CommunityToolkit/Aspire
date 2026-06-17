// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.RustFs.Tests;

public class RustFsPublicApiTests
{
    [Fact]
    public void AddRustFsShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "rustfs";

        var action = () => builder.AddRustFs(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddRustFsShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddRustFs(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<RustFsResource> builder = null!;

        Func<IResourceBuilder<RustFsResource>>? action = null;

        if (useVolume)
        {
            action = () => builder.WithDataVolume();
        }
        else
        {
            const string source = "/data";

            action = () => builder.WithDataBindMount(source);
        }

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMountShouldThrowWhenSourceIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddRustFs("rustfs");

        string source = null!;

        var action = () => resourceBuilder.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Fact]
    public void AddBucketShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<RustFsResource> builder = null!;

        var action = () => builder.AddBucket("mybucket");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddBucketShouldThrowWhenBucketNameIsEmpty()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddRustFs("rustfs");

        var action = () => resourceBuilder.AddBucket(string.Empty);

        Assert.Throws<ArgumentException>(action);
    }

    [Fact]
    public void AddBucketListShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<RustFsResource> builder = null!;
        IReadOnlyList<string> bucketNames = ["mybucket"];

        var action = () => builder.AddBucket(bucketNames);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public async Task VerifyRustFsConnectionStringWithCustomCredentials()
    {
        var builder = DistributedApplication.CreateBuilder();
        var accessKey = "myaccesskey";
        var secretKey = "mysecretkey";
        var accessKeyParam = builder.AddParameter("accessKey", accessKey);
        var secretKeyParam = builder.AddParameter("secretKey", secretKey);
        var rustfs = builder.AddRustFs("rustfs", accessKeyParam, secretKeyParam)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 2000));

        var connectionString = await rustfs.Resource.GetConnectionStringAsync();
        Assert.Equal($"Endpoint=http://localhost:2000;AccessKey={accessKey};SecretKey={secretKey}", connectionString);
    }
}

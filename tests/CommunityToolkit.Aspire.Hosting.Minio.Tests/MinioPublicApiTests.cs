// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Minio.Tests;

public class MinioPublicApiTests
{
    [Fact]
    public void AddMinioContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "Minio";
    
        var action = () => builder.AddMinioContainer(name);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
    
    [Fact]
    public void AddMinioContainerShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;
    
        var action = () => builder.AddMinioContainer(name);
    
        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<MinioContainerResource> builder = null!;

        Func<IResourceBuilder<MinioContainerResource>>? action = null;

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
        var resourceBuilder = builder.AddMinioContainer("minio");

        string source = null!;

        var action = () => resourceBuilder.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }
    
    [Fact]
    public void VerifyMinioContainerResourceWithHostPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddMinioContainer("minio")
            .WithHostPort(1000);

        var resource = Assert.Single(builder.Resources.OfType<MinioContainerResource>());
        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), x => x.Name == "http");
        Assert.Equal(1000, endpoint.Port);
    }

    [Fact]
    public async Task VerifyMinioContainerResourceWithPassword()
    {
        var builder = DistributedApplication.CreateBuilder();
        var password = "p@ssw0rd1";
        var pass = builder.AddParameter("pass", password);
        var minio = builder.AddMinioContainer("minio")
            .WithPassword(pass)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 2000));

        var connectionString = await minio.Resource.GetConnectionStringAsync();
        Assert.Equal("Endpoint=http://localhost:2000;AccessKey=minioadmin;SecretKey=p@ssw0rd1", connectionString);
    }

    [Fact]
    public async Task VerifyMinioContainerResourceWithUserName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var user = "user1";
        var pass = builder.AddParameter("user", user);
        var postgres = builder.AddMinioContainer("minio")
            .WithUserName(pass)
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 2000));

        var connectionString = await postgres.Resource.GetConnectionStringAsync();
        Assert.Equal($"Endpoint=http://localhost:2000;AccessKey=user1;SecretKey={postgres.Resource.PasswordParameter.Value}", connectionString);
    }
}

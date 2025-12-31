// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace CommunityToolkit.Aspire.Hosting.Umami.Tests;

public class AddUmamiTests
{
    [Fact]
    public void AddUmamiAddsGeneratedPasswordParameterWithUserSecretsParameterDefaultInRunMode()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create();

        var umami = appBuilder.AddUmami("umami");

        Assert.Equal("Aspire.Hosting.ApplicationModel.UserSecretsParameterDefault", umami.Resource.SecretParameter.Default?.GetType().FullName);
    }
    
    [Fact]
    public void AddUmamiDoesNotAddGeneratedPasswordParameterWithUserSecretsParameterDefaultInPublishMode()
    {
        using var appBuilder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var umami = appBuilder.AddUmami("umami");

        Assert.NotEqual("Aspire.Hosting.ApplicationModel.UserSecretsParameterDefault", umami.Resource.SecretParameter.Default?.GetType().FullName);
    }

    [Fact]
    public async Task AddUmamiContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
    
        var umami = appBuilder.AddUmami("umami");
    
        using var app = appBuilder.Build();
    
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
    
        var containerResource = Assert.Single(appModel.Resources.OfType<UmamiResource>());
        Assert.Equal("umami", containerResource.Name);
    
        var endpoint = Assert.Single(containerResource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(3000, endpoint.TargetPort);
        Assert.False(endpoint.IsExternal);
        Assert.Equal("http", endpoint.Name);
        Assert.Null(endpoint.Port);
        Assert.Equal(ProtocolType.Tcp, endpoint.Protocol);
        Assert.Equal("http", endpoint.Transport);
        Assert.Equal("http", endpoint.UriScheme);
    
        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(UmamiContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(UmamiContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(UmamiContainerImageTags.Registry, containerAnnotation.Registry);
    
#pragma warning disable CS0618 // Type or member is obsolete
        var config = await umami.Resource.GetEnvironmentVariableValuesAsync();
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.Collection(config,
            env =>
            {
                Assert.Equal("APP_SECRET", env.Key);
                Assert.NotNull(env.Value);
                Assert.True(env.Value.Length >= 8);
            });
    }
}
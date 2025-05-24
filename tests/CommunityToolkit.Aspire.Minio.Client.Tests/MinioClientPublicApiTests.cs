// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Minio.Client.Tests;

public class MinioClientPublicApiTests
{
    [Fact]
    public void AddMinioClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var configurationSectionName = "minio";

        var action = () => builder.AddMinioClient(configurationSectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
    
    [Fact]
    public void AddMinioClientShouldThrowWhenConfigurationSectionNameIsNull()
    {
        IHostApplicationBuilder builder = null!;

        string? configurationSectionName = null;

        var action = () => builder.AddMinioClient(configurationSectionName: configurationSectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CommunityToolkit.Aspire.Minio.Client.Tests;

public class ConfigurationTests
{
    [Fact]
    public void EndpointIsNullByDefault() =>
        Assert.Null(new MinioClientSettings().Endpoint);
    
    [Fact]
    public void CredentialsIsNullByDefault() =>
      Assert.Null(new MinioClientSettings().Credentials);
}

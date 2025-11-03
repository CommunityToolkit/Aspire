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

    [Fact]
    public void UseSslIsFalseByDefault()
    {
        var settings = new MinioClientSettings();
        Assert.False(settings.UseSsl);
    }

    [Fact]
    public void ParseConnectionString_HttpsUri_DoesNotInferUseSsl()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString("https://minio.example.com:9000");
        
        Assert.NotNull(settings.Endpoint);
        Assert.Equal("https://minio.example.com:9000/", settings.Endpoint.ToString());
        Assert.False(settings.UseSsl); // Should remain false since UseSsl was not explicitly set
    }

    [Fact]
    public void ParseConnectionString_HttpUri_DoesNotInferUseSsl()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString("http://minio.example.com:9000");
        
        Assert.NotNull(settings.Endpoint);
        Assert.Equal("http://minio.example.com:9000/", settings.Endpoint.ToString());
        Assert.False(settings.UseSsl); // Should remain false
    }

    [Fact]
    public void ParseConnectionString_ExplicitUseSslTrue()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString("Endpoint=http://minio.example.com:9000;UseSsl=true;AccessKey=key;SecretKey=secret");
        
        Assert.NotNull(settings.Endpoint);
        Assert.True(settings.UseSsl);
    }

    [Fact]
    public void ParseConnectionString_ExplicitUseSslFalse()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString("Endpoint=https://minio.example.com:9000;UseSsl=false;AccessKey=key;SecretKey=secret");
        
        Assert.NotNull(settings.Endpoint);
        Assert.False(settings.UseSsl);
    }

    [Fact]
    public void ParseConnectionString_UseSslCaseInsensitive()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString("Endpoint=http://minio.example.com:9000;UseSsl=True;AccessKey=key;SecretKey=secret");
        
        Assert.True(settings.UseSsl);
    }


    [Fact]
    public void ParseConnectionString_WithCredentials()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString("Endpoint=http://minio.example.com:9000;AccessKey=mykey;SecretKey=mysecret;UseSsl=true");
        
        Assert.NotNull(settings.Endpoint);
        Assert.NotNull(settings.Credentials);
        Assert.Equal("mykey", settings.Credentials.AccessKey);
        Assert.Equal("mysecret", settings.Credentials.SecretKey);
        Assert.True(settings.UseSsl);
    }

    [Fact]
    public void ParseConnectionString_NullOrEmpty_DoesNotThrow()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString(null);
        Assert.Null(settings.Endpoint);
        
        settings = new MinioClientSettings();
        settings.ParseConnectionString("");
        Assert.Null(settings.Endpoint);
        
        settings = new MinioClientSettings();
        settings.ParseConnectionString("   ");
        Assert.Null(settings.Endpoint);
    }

    [Fact]
    public void ParseConnectionString_HttpsEndpointInConnectionString_DoesNotInferUseSsl()
    {
        var settings = new MinioClientSettings();
        settings.ParseConnectionString("Endpoint=https://minio.example.com:9000;AccessKey=key;SecretKey=secret");
        
        Assert.NotNull(settings.Endpoint);
        Assert.Equal("https://minio.example.com:9000/", settings.Endpoint.ToString());
        Assert.False(settings.UseSsl); // Should remain false without explicit UseSsl
    }
}

using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.SeaweedFS.Client.Tests;

public class ConfigurationTests
{
    [Fact]
    public void ParseConnectionString_DbConnectionStringWithoutKeys_SetsEndpointOnly()
    {
        var settings = new SeaweedFSClientSettings();
        settings.ParseConnectionString("Endpoint=https://seaweedfs.local:8333");

        Assert.NotNull(settings.Endpoint);
        Assert.Equal("https://seaweedfs.local:8333/", settings.Endpoint.ToString());
        Assert.Null(settings.AccessKey);
        Assert.Null(settings.SecretKey);
    }

    [Fact]
    public void ParseConnectionString_DbConnectionString_SetsFilerEndpoint()
    {
        var settings = new SeaweedFSClientSettings();
        settings.ParseConnectionString("FilerEndpoint=http://localhost:8888;AccessKey=admin");

        Assert.NotNull(settings.FilerEndpoint);
        Assert.Equal("http://localhost:8888/", settings.FilerEndpoint.ToString());
    }

    [Fact]
    public void ParseConnectionString_DbConnectionString_SetsFilerUrl()
    {
        // Tests the implicit fallback property injected by the Aspire AppHost (IResourceWithConnectionString)
        var settings = new SeaweedFSClientSettings();
        settings.ParseConnectionString("FilerUrl=http://seaweedfs:8888;SecretKey=secret");

        Assert.NotNull(settings.FilerEndpoint);
        Assert.Equal("http://seaweedfs:8888/", settings.FilerEndpoint.ToString());
    }


    [Fact]
    public void DefaultValuesAreCorrect()
    {
        var settings = new SeaweedFSClientSettings();

        Assert.Null(settings.Endpoint);
        Assert.Null(settings.AccessKey);
        Assert.Null(settings.SecretKey);

        Assert.True(settings.ForcePathStyle);
        Assert.False(settings.DisableHealthChecks);
        Assert.False(settings.UseSsl);
    }

    [Fact]
    public void ParseConnectionString_NullOrEmpty_DoesNotThrow()
    {
        var settings = new SeaweedFSClientSettings();

        settings.ParseConnectionString(null);
        Assert.Null(settings.Endpoint);

        settings.ParseConnectionString("");
        Assert.Null(settings.Endpoint);

        settings.ParseConnectionString("   ");
        Assert.Null(settings.Endpoint);
    }

    [Fact]
    public void ParseConnectionString_AbsoluteUri_SetsEndpoint()
    {
        var settings = new SeaweedFSClientSettings();
        settings.ParseConnectionString("http://localhost:8333");

        Assert.NotNull(settings.Endpoint);
        Assert.Equal("http://localhost:8333/", settings.Endpoint.ToString());
    }

    [Fact]
    public void ParseConnectionString_DbConnectionString_SetsProperties()
    {
        var settings = new SeaweedFSClientSettings();
        settings.ParseConnectionString("Endpoint=http://localhost:8333;AccessKey=admin;SecretKey=secret");

        Assert.NotNull(settings.Endpoint);
        Assert.Equal("http://localhost:8333/", settings.Endpoint.ToString());
        Assert.Equal("admin", settings.AccessKey);
        Assert.Equal("secret", settings.SecretKey);
    }

    [Fact]
    public void ParseConnectionString_UseSslCaseInsensitive()
    {
        var settings = new SeaweedFSClientSettings();
        settings.ParseConnectionString("Endpoint=http://localhost:8333;UseSsl=True;AccessKey=key;SecretKey=secret");

        Assert.True(settings.UseSsl);
    }

    [Fact]
    public void ParseConnectionString_ExplicitUseSslFalse()
    {
        var settings = new SeaweedFSClientSettings();
        settings.ParseConnectionString("Endpoint=https://localhost:8333;UseSsl=false;AccessKey=key;SecretKey=secret");

        Assert.NotNull(settings.Endpoint);
        Assert.False(settings.UseSsl);
    }
}
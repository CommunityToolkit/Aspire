namespace CommunityToolkit.Aspire.VLLM.Tests;

public class VLLMClientSettingsTests
{
    [Theory]
    [InlineData("http://localhost:8000")]
    [InlineData("Endpoint=http://localhost:8000")]
    public void ParseConnectionStringSetsEndpoint(string connectionString)
    {
        var settings = new VLLMClientSettings();
        settings.ParseConnectionString(connectionString);

        Assert.Equal(new Uri("http://localhost:8000"), settings.Endpoint);
    }

    [Fact]
    public void ParseConnectionStringSetsEndpointKeyAndModel()
    {
        var settings = new VLLMClientSettings();
        settings.ParseConnectionString("Endpoint=https://host:8000;Key=secret;Model=my-model");

        Assert.Equal(new Uri("https://host:8000"), settings.Endpoint);
        Assert.Equal("secret", settings.Key);
        Assert.Equal("my-model", settings.Model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseConnectionStringWithEmptyValueLeavesEndpointNull(string? connectionString)
    {
        var settings = new VLLMClientSettings();
        settings.ParseConnectionString(connectionString);

        Assert.Null(settings.Endpoint);
    }
}

namespace CommunityToolkit.Aspire.Hosting.Logto.Client.Tests;

public class LogtoConnectionStringHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetEndpointFromConnectionString_ReturnsNull_WhenConnectionStringIsNullOrWhiteSpace(string? connectionString)
    {
        var result = LogtoConnectionStringHelper.GetEndpointFromConnectionString(connectionString);
        
        Assert.Null(result);
    }

    [Fact]
    public void GetEndpointFromConnectionString_ReturnsSameString_WhenItIsValidUri()
    {
        var connectionString = "https://logto.example.com/";
        
        var result = LogtoConnectionStringHelper.GetEndpointFromConnectionString(connectionString);
        
        Assert.Equal(connectionString, result);
    }

    [Fact]
    public void GetEndpointFromConnectionString_ReturnsEndpoint_WhenEndpointKeyExistsInConnectionString()
    {
        var connectionString =
            "Endpoint=https://logto.example.com;SomeOtherKey=SomeValue";

        var result = LogtoConnectionStringHelper.GetEndpointFromConnectionString(connectionString);
        
        Assert.Equal("https://logto.example.com/", result); 
    }

    [Fact]
    public void GetEndpointFromConnectionString_ReturnsNull_WhenEndpointIsNotValidUri()
    {
        var connectionString =
            "Endpoint=not-a-valid-uri;SomeOtherKey=SomeValue";
        
        var result = LogtoConnectionStringHelper.GetEndpointFromConnectionString(connectionString);
        
        Assert.Null(result);
    }

    [Fact]
    public void GetEndpointFromConnectionString_ReturnsNull_WhenEndpointKeyMissing()
    {
        var connectionString =
            "Server=localhost;User Id=sa;Password=123;";
        
        var result = LogtoConnectionStringHelper.GetEndpointFromConnectionString(connectionString);
        
        Assert.Null(result);
    }
}
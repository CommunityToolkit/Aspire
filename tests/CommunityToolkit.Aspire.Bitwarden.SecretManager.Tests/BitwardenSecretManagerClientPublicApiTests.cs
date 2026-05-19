using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Bitwarden.SecretManager.Tests;

public class BitwardenSecretManagerClientPublicApiTests
{
    [Fact]
    public void AddBitwardenSecretManagerClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var action = () => builder.AddBitwardenSecretManagerClient("bitwarden");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddBitwardenSecretManagerClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string connectionName = null!;

        var action = () => builder.AddBitwardenSecretManagerClient(connectionName);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Fact]
    public void AddBitwardenSecretManagerClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        var action = () => builder.AddBitwardenSecretManagerClient(string.Empty);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("connectionName", exception.ParamName);
    }

    [Fact]
    public void AddKeyedBitwardenSecretManagerClientShouldThrowWhenBuilderIsNull()
    {
        IHostApplicationBuilder builder = null!;

        var action = () => builder.AddKeyedBitwardenSecretManagerClient("bitwarden");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddKeyedBitwardenSecretManagerClientShouldThrowWhenNameIsNull()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        string name = null!;

        var action = () => builder.AddKeyedBitwardenSecretManagerClient(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void AddKeyedBitwardenSecretManagerClientShouldThrowWhenNameIsEmpty()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        var action = () => builder.AddKeyedBitwardenSecretManagerClient(string.Empty);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal("name", exception.ParamName);
    }
}
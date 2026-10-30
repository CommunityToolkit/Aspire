using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Chroma.Tests;

public class ChromaClientPublicApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddChromaClientShouldThrowWhenBuilderIsNull(bool useKeyed)
    {
        IHostApplicationBuilder builder = null!;

        Action action = useKeyed
            ? () => builder.AddKeyedChromaClient("chroma")
            : () => builder.AddChromaClient("chroma");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddChromaClientShouldThrowWhenConnectionNameIsNullOrEmpty(string? connectionName)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        var action = () => builder.AddChromaClient(connectionName!);

        var exception = Assert.ThrowsAny<ArgumentException>(action);
        Assert.Equal(nameof(connectionName), exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddKeyedChromaClientShouldThrowWhenNameIsNullOrEmpty(string? name)
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        var action = () => builder.AddKeyedChromaClient(name!);

        var exception = Assert.ThrowsAny<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }
}

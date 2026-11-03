using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Chroma.Tests;

public class ChromaPublicApiTests
{
    [Fact]
    public void AddChromaShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var action = () => builder.AddChroma("chroma");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddChromaShouldThrowWhenNameIsNullOrEmpty(string? name)
    {
        var builder = DistributedApplication.CreateBuilder();

        var action = () => builder.AddChroma(name!);

        var exception = Assert.ThrowsAny<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<ChromaResource> builder = null!;

        Func<IResourceBuilder<ChromaResource>> action = useVolume
            ? () => builder.WithDataVolume()
            : () => builder.WithDataBindMount("/data");

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithDataBindMountShouldThrowWhenSourceIsNullOrEmpty(string? source)
    {
        var builder = DistributedApplication.CreateBuilder().AddChroma("chroma");

        var action = () => builder.WithDataBindMount(source!);

        var exception = Assert.ThrowsAny<ArgumentException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }
}

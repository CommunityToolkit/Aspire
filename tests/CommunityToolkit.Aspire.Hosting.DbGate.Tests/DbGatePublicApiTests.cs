using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.DbGate.Tests;

public class DbGatePublicApiTests
{
    [Fact]
    public void AddDbGateContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "dbgate";

        var action = () => builder.AddDbGate(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddDbGateContainerShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddDbGate(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithDataShouldThrowWhenBuilderIsNull(bool useVolume)
    {
        IResourceBuilder<DbGateContainerResource> builder = null!;

        Func<IResourceBuilder<DbGateContainerResource>>? action = null;

        if (useVolume)
        {
            action = () => builder.WithDataVolume();
        }
        else
        {
            const string source = "/data";

            action = () => builder.WithDataBindMount(source);
        }

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMountShouldThrowWhenSourceIsNull()
    {
        var builder = new DistributedApplicationBuilder([]);
        var resourceBuilder = builder.AddDbGate("dbgate");

        string source = null!;

        var action = () => resourceBuilder.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }

    [Fact]
    public void WithHostPortShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<DbGateContainerResource> builder = null!;

        Func<IResourceBuilder<DbGateContainerResource>>? action = null;

        action = () => builder.WithHostPort(9090);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
}

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Dbx.Tests;

public class DbxPublicApiTests
{
    [Fact]
    public void AddDbxContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var action = () => builder.AddDbx();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddDbxContainerShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddDbx(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithHostPortShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<DbxContainerResource> builder = null!;

        Func<IResourceBuilder<DbxContainerResource>>? action = null;

        action = () => builder.WithHostPort(9090);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
}

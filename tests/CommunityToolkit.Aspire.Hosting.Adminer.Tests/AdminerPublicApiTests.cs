using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Adminer.Tests;

public class AdminerPublicApiTests
{
    [Fact]
    public void AddAdminerContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "adminer";

        var action = () => builder.AddAdminer(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddAdminerContainerShouldThrowWhenNameIsNull()
    {
        IDistributedApplicationBuilder builder = new DistributedApplicationBuilder([]);
        string name = null!;

        var action = () => builder.AddAdminer(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithHostPortShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<AdminerContainerResource> builder = null!;

        Func<IResourceBuilder<AdminerContainerResource>>? action = null;

        action = () => builder.WithHostPort(9090);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }
}

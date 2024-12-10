namespace CommunityToolkit.Aspire.Hosting.HomeAssistant.Tests;

public class HomeAssistantPublicApiTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void AddHomeAssistantContainerShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        const string name = "home-assistant";

        var action = () => builder.AddHomeAssistant(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void AddHomeAssistantContainerShouldThrowWhenNameIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        const string name = null!;

        var action = () => builder.AddHomeAssistant(name);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }


    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public void AddHomeAssistantContainerShouldThrowWhenNameIsEmptyOrWhiteSpace(string name)
    {
        var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var action = () => builder.AddHomeAssistant(name);

        var exception = Assert.Throws<ArgumentException>(action);
        Assert.Equal(nameof(name), exception.ParamName);
    }

    [Fact]
    public void WithDataVolumeShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<HomeAssistantResource> builder = null!;

        var action = () => builder.WithDataVolume();

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMountShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<HomeAssistantResource> builder = null!;
        const string source = "/configuration";

        var action = () => builder.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(builder), exception.ParamName);
    }

    [Fact]
    public void WithDataBindMountShouldThrowWhenSourceIsNull()
    {
        var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        var homeAssistant = builder.AddHomeAssistant("home-assistant");
        string source = null!;

        var action = () => homeAssistant.WithDataBindMount(source);

        var exception = Assert.Throws<ArgumentNullException>(action);
        Assert.Equal(nameof(source), exception.ParamName);
    }
}

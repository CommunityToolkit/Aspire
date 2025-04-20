using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Bun.Tests;

public class AddBunAppTests
{
    [Fact]
    public void BunAppUsesBunCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddBunApp("bun");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<BunAppResource>());

        Assert.Equal("bun", resource.Command);
    }

    [Fact]
    public async Task BunAppDefaultArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddBunApp("bun");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<BunAppResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("index.ts", arg)
        );
    }

    [Fact]
    public async Task BunAppWatchArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddBunApp("bun", watch: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<BunAppResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("--watch", arg),
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("index.ts", arg)
        );
    }

    [Fact]
    public async Task BunAppWithCustomEntryPoint()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddBunApp("bun", entryPoint: "app.ts");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<BunAppResource>());

        var args = await resource.GetArgumentValuesAsync();

        Assert.Collection(args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("app.ts", arg)
        );
    }

    [Fact]
    public void BunAppWithWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddBunApp("bun", workingDirectory: "src");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<BunAppResource>());

        Assert.EndsWith("src", resource.WorkingDirectory);
    }

    [Fact]
    public void AddBunNullBuilderThrows()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddBunApp("bun"));
    }

    [Fact]
    public void AddBunNullNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddBunApp(null!));
    }

    [Fact]
    public void AddBunEmptyNameThrows()
    {
        var builder = DistributedApplication.CreateBuilder();
        var name = "";

        Assert.Throws<ArgumentException>(() => builder.AddBunApp(name));
    }

    [Fact]
    public void AddBunNullEntryPointThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddBunApp("bun", entryPoint: null!));
    }

    [Fact]
    public void AddBunEmptyEntryPointThrows()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentException>(() => builder.AddBunApp("bun", entryPoint: ""));
    }
}

using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Golang.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void DefaultGolangApp()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GolangAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("go", resource.Command);
    }

    [Fact]
    public async Task GolangAppWithBuildTagsAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api", buildTags: ["dev"]);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GolangAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        var args = await resource.GetArgumentListAsync();
        Assert.Collection(
            args,
            arg =>
            {
                Assert.Equal("run", arg);
            },
            arg =>
            {
                Assert.Equal("-tags", arg);
            },
            arg =>
            {
                Assert.Equal("dev", arg);
            },
            arg =>
            {
                Assert.Equal(".", arg);
            }
        );
    }


    [Fact]
    public async Task GolangAppWithExecutableAsync()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api", "./cmd/server");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GolangAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        var args = await resource.GetArgumentListAsync();
        Assert.Collection(
            args,
            arg =>
            {
                Assert.Equal("run", arg);
            },
            arg =>
            {
                Assert.Equal("./cmd/server", arg);
            }
        );
    }
}
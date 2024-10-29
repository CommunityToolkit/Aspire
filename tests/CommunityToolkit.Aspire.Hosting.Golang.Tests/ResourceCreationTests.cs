using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Golang.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void DefaultViteAppUsesNpm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddGolangApp("golang", "../../examples/golang/gin-api");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<GolangAppExecutableResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("go", resource.Command);
    }
}
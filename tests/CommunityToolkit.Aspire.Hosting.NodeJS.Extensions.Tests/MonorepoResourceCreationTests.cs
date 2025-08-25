using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class MonorepoResourceCreationTests
{
    [Fact]
    public void AddNxApp_CreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("test-nx", workingDirectory: "../test-nx");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());
        
        Assert.Equal("test-nx", nxResource.Name);
        Assert.NotEmpty(nxResource.WorkingDirectory);
    }

    [Fact]
    public void AddTurborepoApp_CreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("test-turbo", workingDirectory: "../test-turbo");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var turborepoResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());
        
        Assert.Equal("test-turbo", turborepoResource.Name);
        Assert.NotEmpty(turborepoResource.WorkingDirectory);
    }

    [Fact]
    public void NxResource_DefaultWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nx = builder.AddNxApp("my-nx");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nxResource = Assert.Single(appModel.Resources.OfType<NxResource>());
        
        Assert.Contains("my-nx", nxResource.WorkingDirectory);
    }

    [Fact]
    public void TurborepoResource_DefaultWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        var turbo = builder.AddTurborepoApp("my-turbo");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var turborepoResource = Assert.Single(appModel.Resources.OfType<TurborepoResource>());
        
        Assert.Contains("my-turbo", turborepoResource.WorkingDirectory);
    }
}
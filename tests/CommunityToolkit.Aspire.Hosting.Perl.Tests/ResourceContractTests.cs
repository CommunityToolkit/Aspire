using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class ResourceContractTests
{
    [Fact]
    public void AddPerlScriptImplementsIResourceWithServiceDiscovery()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.IsAssignableFrom<IResourceWithServiceDiscovery>(resource);
    }

    [Fact]
    public void AddPerlScriptIsExecutableResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.IsAssignableFrom<ExecutableResource>(resource);
    }
}

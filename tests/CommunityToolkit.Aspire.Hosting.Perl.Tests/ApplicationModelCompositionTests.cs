using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class ApplicationModelCompositionTests
{
    [Fact]
    public void MultipleResourcesCanBeAddedToModel()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("script-app", "scripts", "worker.pl");
        builder.AddPerlApi("api-app", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resources = appModel.Resources.OfType<PerlAppResource>().ToList();

        Assert.Equal(2, resources.Count);
        Assert.Contains(resources, r => r.Name == "script-app");
        Assert.Contains(resources, r => r.Name == "api-app");
    }
}

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

    [Fact]
    public void MultipleResourcesWithSharedPackages_ModelBuildsSuccessfully()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("script-app", "scripts", "worker.pl")
            .WithCpanMinus()
            .WithPackage("OpenTelemetry::SDK")
            .WithPackage("DBI");

        builder.AddPerlApi("api-app", "api", "server.pl")
            .WithCpanMinus()
            .WithPackage("OpenTelemetry::SDK")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlResources = appModel.Resources.OfType<PerlAppResource>().ToList();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();

        Assert.Equal(2, perlResources.Count);
        Assert.Equal(4, installers.Count);

        var installerNames = installers.Select(i => i.Name).ToList();
        Assert.Equal(installerNames.Count, installerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

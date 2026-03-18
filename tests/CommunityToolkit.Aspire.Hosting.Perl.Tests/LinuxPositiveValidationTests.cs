using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Services;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIRECOMMAND001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class LinuxPositiveValidationTests
{
    [Fact, RequiresLinux]
    public async Task IsPerlInstalledAsync_ReturnsTrue_WhenPerlIsInstalled()
    {
        var manager = new PerlInstallationManager();

        var result = await manager.IsPerlInstalledAsync("perl");

        Assert.True(result);
    }

    [Fact, RequiresLinux]
    public async Task ValidationCallback_ReturnsSuccess_WhenPerlIsInstalled()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = resource.Annotations.OfType<RequiredCommandAnnotation>().Single(a => a.Command == "perl");
        Assert.NotNull(annotation.ValidationCallback);

        var context = new RequiredCommandValidationContext("perl", app.Services, CancellationToken.None);
        var result = await annotation.ValidationCallback(context);

        Assert.True(result.IsValid);
    }
}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Services;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIRECOMMAND001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class RegistrationAndCommandTests
{
    [Fact]
    public void PerlInstallationManagerIsRegisteredAsSingleton()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var manager1 = app.Services.GetRequiredService<PerlInstallationManager>();
        var manager2 = app.Services.GetRequiredService<PerlInstallationManager>();

        Assert.NotNull(manager1);
        Assert.Same(manager1, manager2);
    }

    [Fact]
    public void AddPerlScriptAddsRequiredCommandAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations.OfType<RequiredCommandAnnotation>().ToList();
        Assert.Equal(2, annotations.Count);
        Assert.Contains(annotations, a => a.Command == "perl" && a.HelpLink == "https://www.perl.org/get.html");
        Assert.Contains(annotations, a => a.Command == "cpan" && a.HelpLink == "https://metacpan.org/pod/CPAN");
    }

    [Fact]
    public void AddPerlApiAddsRequiredCommandAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations.OfType<RequiredCommandAnnotation>().ToList();
        Assert.Equal(2, annotations.Count);
        Assert.Contains(annotations, a => a.Command == "perl");
        Assert.Contains(annotations, a => a.Command == "cpan");
    }

    [Fact]
    public void RequiredCommandAnnotationHasValidationCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = resource.Annotations.OfType<RequiredCommandAnnotation>().Single(a => a.Command == "perl");
        Assert.NotNull(annotation.ValidationCallback);
    }
}

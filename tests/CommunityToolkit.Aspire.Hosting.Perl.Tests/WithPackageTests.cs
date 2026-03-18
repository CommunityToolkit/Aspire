using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithPackageTests
{
    [Fact]
    public void WithPackageCreatesInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Contains("Mojolicious", installerResource.Name);
    }

    [Fact]
    public void WithPackage_ModuleNameWithColons_SanitizesInstallerResourceName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("OpenTelemetry::SDK");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Equal("OpenTelemetry88SDK-installer", installerResource.Name);
    }

    [Fact]
    public void WithPackageAddsRequiredModuleAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("DBI");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotations = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Equal("DBI", annotations[0].Name);
        Assert.False(annotations[0].Force);
        Assert.False(annotations[0].SkipTest);
    }

    [Fact]
    public void WithPackageWithForceAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("DBI", force: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Single();
        Assert.True(annotation.Force);
    }

    [Fact]
    public void WithPackageWithSkipTestAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("DBI", skipTest: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Single();
        Assert.True(annotation.SkipTest);
    }

    [Fact]
    public void WithPackageMultiplePackagesCreateMultipleInstallers()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious")
            .WithPackage("DBI")
            .WithPackage("JSON::XS");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Equal(3, installers.Count);
    }

    [Fact]
    public void WithPackageShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithPackage("Mojolicious"));
    }

    [Fact]
    public void WithPackageShouldThrowWhenPackageNameIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        Assert.Throws<ArgumentNullException>(() => resource.WithPackage(null!));
        Assert.Throws<ArgumentException>(() => resource.WithPackage(""));
    }

    [Fact]
    public void AddPerlApiAlsoHasDefaultCpanPackageManager()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpan", annotation.ExecutableName);
    }

    [Fact]
    public void AddPerlApiWithCpanMinusAndPackage()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious", skipTest: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpanm", annotation.ExecutableName);

        var moduleAnnotation = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Single();
        Assert.Equal("Mojolicious", moduleAnnotation.Name);
        Assert.True(moduleAnnotation.SkipTest);
    }
}

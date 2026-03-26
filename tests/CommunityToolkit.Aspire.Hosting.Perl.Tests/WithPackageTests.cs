using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithPackageTests
{
    [Theory]
    [InlineData("Mojolicious", "Mojolicious-installer")]
    [InlineData("OpenTelemetry::SDK", "OpenTelemetry88SDK-installer")]
    public void WithPackage_CreatesInstallerWithCorrectName(string moduleName, string expectedInstallerName)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage(moduleName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Equal(expectedInstallerName, installerResource.Name);
    }

    [Theory]
    [InlineData("DBI", false, false)]
    [InlineData("DBI", true, false)]
    [InlineData("DBI", false, true)]
    public void WithPackage_AddsRequiredModuleAnnotationWithCorrectFlags(string moduleName, bool force, bool skipTest)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage(moduleName, force: force, skipTest: skipTest);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = Assert.Single(resource.Annotations.OfType<PerlRequiredModuleAnnotation>());
        Assert.Equal(moduleName, annotation.Name);
        Assert.Equal(force, annotation.Force);
        Assert.Equal(skipTest, annotation.SkipTest);
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

        var annotation = Assert.Single(resource.Annotations.OfType<PerlPackageManagerAnnotation>());
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

        var annotation = Assert.Single(resource.Annotations.OfType<PerlPackageManagerAnnotation>());
        Assert.Equal("cpanm", annotation.ExecutableName);

        var moduleAnnotation = resource.Annotations.OfType<PerlRequiredModuleAnnotation>().Single();
        Assert.Equal("Mojolicious", moduleAnnotation.Name);
        Assert.True(moduleAnnotation.SkipTest);
    }
}

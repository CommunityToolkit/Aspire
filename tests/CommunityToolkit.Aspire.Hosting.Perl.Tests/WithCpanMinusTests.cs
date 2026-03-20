using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithCpanMinusTests
{
    [Fact]
    public void WithCpanMinusChangesPackageManagerToCpanm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = Assert.Single(resource.Annotations.OfType<PerlPackageManagerAnnotation>());
        Assert.Equal("cpanm", annotation.ExecutableName);
    }

    [Fact]
    public void WithCpanMinusReplacesDefaultAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // Should have exactly one PerlPackageManagerAnnotation, not two
        var annotations = resource.Annotations.OfType<PerlPackageManagerAnnotation>().ToList();
        Assert.Single(annotations);
        Assert.Equal("cpanm", annotations[0].ExecutableName);
    }

    [Fact]
    public void WithCpanMinusShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithCpanMinus());
    }

    [Fact]
    public void WithCpanMinus_ThenWithPackage_UsesCpanm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlResource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = Assert.Single(perlResource.Annotations.OfType<PerlPackageManagerAnnotation>());
        Assert.Equal("cpanm", annotation.ExecutableName);

        Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
    }

    [Fact]
    public void WithPackage_WithoutCpanMinus_UsesCpan()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlResource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = Assert.Single(perlResource.Annotations.OfType<PerlPackageManagerAnnotation>());
        Assert.Equal("cpan", annotation.ExecutableName);

        Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
    }
}

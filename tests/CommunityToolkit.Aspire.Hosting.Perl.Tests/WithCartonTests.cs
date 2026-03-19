using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithCartonTests
{
    [Fact]
    public void WithCartonChangesPackageManagerToCarton()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCarton();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = Assert.Single(resource.Annotations.OfType<PerlPackageManagerAnnotation>());
        Assert.Equal(PerlPackageManager.Carton, annotation.PackageManager);
        Assert.Equal("carton", annotation.ExecutableName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithCarton_ReplacesExistingPackageManagerAnnotation(bool withPriorCpanMinus)
    {
        var builder = DistributedApplication.CreateBuilder();

        var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl");
        if (withPriorCpanMinus)
        {
            resource.WithCpanMinus();
        }
        resource.WithCarton();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlResource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = Assert.Single(perlResource.Annotations.OfType<PerlPackageManagerAnnotation>());
        Assert.Equal(PerlPackageManager.Carton, annotation.PackageManager);
        Assert.Equal("carton", annotation.ExecutableName);
    }

    [Fact]
    public void WithCartonShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithCarton());
    }

    [Fact]
    public void WithCartonThenWithProjectDependencies_UsesCarton()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCarton()
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        // Carton should NOT be auto-switched to cpanm
        var annotation = Assert.Single(resource.Annotations.OfType<PerlPackageManagerAnnotation>());
        Assert.Equal(PerlPackageManager.Carton, annotation.PackageManager);

        // Project installer should still be created
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Single(installers);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithCartonAndWithPackage_ThrowsRegardlessOfOrder(bool cartonFirst)
    {
        var builder = DistributedApplication.CreateBuilder();

        var ex = Assert.Throws<NotSupportedException>(() =>
        {
            var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl");
            if (cartonFirst)
            {
                resource.WithCarton().WithPackage("Mojolicious");
            }
            else
            {
                resource.WithPackage("Mojolicious").WithCarton();
            }
        });

        Assert.Contains("WithPackage() and WithCarton() cannot be combined", ex.Message);
        Assert.Contains("WithProjectDependencies()", ex.Message);
    }

    [Fact]
    public void IsCommandAvailable_ReturnsTrueWhenFoundOnPath()
    {
        var fakePath = "/opt/perl/bin" + Path.PathSeparator + "/usr/local/bin";
        Func<string, bool> fileExists = path => path == Path.Combine("/opt/perl/bin", "cpanm");

        var result = PerlAppResourceBuilderExtensions.IsCommandAvailable("cpanm", fakePath, fileExists);

        Assert.True(result);
    }

    [Fact]
    public void IsCommandAvailable_ReturnsFalseWhenNotOnPath()
    {
        var fakePath = "/opt/perl/bin" + Path.PathSeparator + "/usr/local/bin";
        Func<string, bool> fileExists = _ => false;

        var result = PerlAppResourceBuilderExtensions.IsCommandAvailable("cpanm", fakePath, fileExists);

        Assert.False(result);
    }

    [Fact]
    public void IsCommandAvailable_ReturnsTrueWhenAbsolutePathExists()
    {
        Func<string, bool> fileExists = path => path == "/usr/bin/cpanm";

        var result = PerlAppResourceBuilderExtensions.IsCommandAvailable("/usr/bin/cpanm", null, fileExists);

        Assert.True(result);
    }

    [Fact]
    public void TryResolveCommandFromPath_ReturnsFullPathWhenFound()
    {
        var fakePath = "/first/dir" + Path.PathSeparator + "/second/dir";
        Func<string, bool> fileExists = path => path == Path.Combine("/second/dir", "carton");

        var result = PerlAppResourceBuilderExtensions.TryResolveCommandFromPath("carton", fakePath, fileExists);

        Assert.Equal(Path.Combine("/second/dir", "carton"), result);
    }

    [Fact]
    public void TryResolveCommandFromPath_ReturnsNullWhenNotFound()
    {
        var fakePath = "/first/dir" + Path.PathSeparator + "/second/dir";
        Func<string, bool> fileExists = _ => false;

        var result = PerlAppResourceBuilderExtensions.TryResolveCommandFromPath("carton", fakePath, fileExists);

        Assert.Null(result);
    }

    [Fact]
    public void TryResolveCommandFromPath_ReturnsNullWhenPathIsEmpty()
    {
        Func<string, bool> fileExists = _ => true;

        var result = PerlAppResourceBuilderExtensions.TryResolveCommandFromPath("cpanm", null, fileExists);

        Assert.Null(result);
    }
}

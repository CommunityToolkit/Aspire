using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class SetupDependencyTests
{
    [Fact]
    public void SetupDependencies_PerPackageInstallerWaitsForProjectInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithPackage("Extra::Module");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlApp = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        PerlAppResourceBuilderExtensions.SetupDependencies(builder, perlApp);

        var projectAnnotation = perlApp.Annotations.OfType<PerlProjectInstallerAnnotation>().Single();
        var moduleAnnotation = perlApp.Annotations.OfType<PerlModuleInstallerAnnotation>().Single();

        var waitAnnotations = moduleAnnotation.Resource.Annotations.OfType<WaitAnnotation>()
            .Where(w => w.Resource == projectAnnotation.Resource)
            .ToList();

        Assert.Single(waitAnnotations);
    }

    [Fact]
    public void SetupDependencies_NoProjectInstaller_DoesNothing()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlApp = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        PerlAppResourceBuilderExtensions.SetupDependencies(builder, perlApp);

        var moduleAnnotation = perlApp.Annotations.OfType<PerlModuleInstallerAnnotation>().Single();
        var waitAnnotations = moduleAnnotation.Resource.Annotations.OfType<WaitAnnotation>().ToList();

        Assert.DoesNotContain(waitAnnotations, w =>
            w.Resource.Name.Contains("deps-installer"));
    }

    [Fact]
    public void SetupDependencies_MultiplePackages_AllWaitForProjectInstaller()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithPackage("DBI")
            .WithPackage("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var perlApp = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        PerlAppResourceBuilderExtensions.SetupDependencies(builder, perlApp);

        var projectAnnotation = perlApp.Annotations.OfType<PerlProjectInstallerAnnotation>().Single();
        var moduleAnnotations = perlApp.Annotations.OfType<PerlModuleInstallerAnnotation>().ToList();

        Assert.Equal(2, moduleAnnotations.Count);

        var waitCount = moduleAnnotations
            .SelectMany(m => m.Resource.Annotations.OfType<WaitAnnotation>())
            .Count(w => w.Resource == projectAnnotation.Resource);

        Assert.Equal(moduleAnnotations.Count, waitCount);
    }
}

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

        Assert.NotEmpty(waitAnnotations);
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

        foreach (var moduleAnnotation in perlApp.Annotations.OfType<PerlModuleInstallerAnnotation>())
        {
            var hasWaitForProject = moduleAnnotation.Resource.Annotations
                .OfType<WaitAnnotation>()
                .Any(w => w.Resource == projectAnnotation.Resource);

            Assert.True(hasWaitForProject, $"Installer '{moduleAnnotation.Resource.Name}' should wait for project deps installer");
        }
    }
}

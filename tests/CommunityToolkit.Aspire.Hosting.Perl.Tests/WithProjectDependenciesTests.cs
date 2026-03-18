using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class WithProjectDependenciesTests
{
    [Fact]
    public void WithProjectDependencies_CreatesProjectInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Equal("perl-app-deps-installer", installerResource.Name);
    }

    [Fact]
    public void WithProjectDependencies_AutoSwitchesToCpanm_WhenDefaultCpan()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal(PerlPackageManager.Cpanm, annotation.PackageManager);
    }

    [Fact]
    public void WithProjectDependencies_KeepsCpanm_WhenAlreadyCpanm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanMinus()
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal(PerlPackageManager.Cpanm, annotation.PackageManager);
    }

    [Fact]
    public void WithProjectDependencies_AddsProjectInstallerAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlProjectInstallerAnnotation>(out var annotation));
        Assert.NotNull(annotation.Resource);
    }

    [Fact]
    public void WithProjectDependencies_IsIdempotent()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithProjectDependencies();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Single(installers);
    }

    [Fact]
    public void WithProjectDependencies_ShouldThrowWhenBuilderIsNull()
    {
        IResourceBuilder<PerlAppResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithProjectDependencies());
    }

#pragma warning disable ASPIRECOMMAND001
    [Fact]
    public void WithProjectDependencies_CartonDeployment_WarnsMissingSnapshot()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            // Create a cpanfile but NO cpanfile.snapshot
            File.WriteAllText(Path.Combine(tempDir.FullName, "cpanfile"), "requires 'Mojolicious';");

            var builder = DistributedApplication.CreateBuilder();

            builder.AddPerlScript("perl-app", tempDir.FullName, "app.pl")
                .WithCarton()
                .WithProjectDependencies(cartonDeployment: true);

            using var app = builder.Build();

            var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
            var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

            // The deployment annotation should be stored
            Assert.True(resource.TryGetLastAnnotation<CommunityToolkit.Aspire.Hosting.Perl.Annotations.PerlCartonDeploymentAnnotation>(out var annotation));
            Assert.True(annotation.UseDeployment);

            // A RequiredCommandAnnotation for cpanfile.snapshot should be added since the snapshot is missing
            var snapshotCheck = resource.Annotations
                .OfType<RequiredCommandAnnotation>()
                .SingleOrDefault(a => a.Command == "cpanfile.snapshot");
            Assert.NotNull(snapshotCheck);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void WithProjectDependencies_CartonDeployment_NoWarningWhenSnapshotExists()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            // Create both cpanfile AND cpanfile.snapshot
            File.WriteAllText(Path.Combine(tempDir.FullName, "cpanfile"), "requires 'Mojolicious';");
            File.WriteAllText(Path.Combine(tempDir.FullName, "cpanfile.snapshot"), "DISTRIBUTIONS\n");

            var builder = DistributedApplication.CreateBuilder();

            builder.AddPerlScript("perl-app", tempDir.FullName, "app.pl")
                .WithCarton()
                .WithProjectDependencies(cartonDeployment: true);

            using var app = builder.Build();

            var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
            var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

            // No cpanfile.snapshot RequiredCommandAnnotation should be present since the file exists
            var snapshotCheck = resource.Annotations
                .OfType<RequiredCommandAnnotation>()
                .SingleOrDefault(a => a.Command == "cpanfile.snapshot");
            Assert.Null(snapshotCheck);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

#pragma warning restore ASPIRECOMMAND001
    [Fact]
    public void WithProjectDependencies_CombinesWithPerPackageInstallers()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithPackage("Extra::Module");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installers = appModel.Resources.OfType<PerlModuleInstallerResource>().ToList();
        Assert.Equal(2, installers.Count);
    }

    [Fact]
    public void WithCartonAndLocalLib_ProjectInstallerGetsLocalLibEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCarton()
            .WithProjectDependencies()
            .WithLocalLib("vendor");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        var envCallbacks = installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envCallbacks);
    }

    [Fact]
    public void WithLocalLibWithoutCarton_ProjectInstallerStillGetsLocalLibEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithProjectDependencies()
            .WithLocalLib("vendor");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

        var envCallbacks = installerResource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();
        Assert.NotEmpty(envCallbacks);
    }
}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class RegistrationAndCommandTests
{
    [Theory]
    [InlineData("script")]
    [InlineData("api")]
    [InlineData("module")]
    [InlineData("executable")]
    public void AddPerlApp_WithoutDependencies_OnlyRequiresPerl(string entrypoint)
    {
        using var tempDir = new TempDirectory();
        var builder = DistributedApplication.CreateBuilder();

        _ = entrypoint switch
        {
            "script" => builder.AddPerlScript("perl-app", tempDir.Path, "app.pl"),
            "api" => builder.AddPerlApi("perl-app", tempDir.Path, "app.pl"),
            "module" => builder.AddPerlModule("perl-app", tempDir.Path, "App::Main"),
            "executable" => builder.AddPerlExecutable("perl-app", tempDir.Path, "app"),
            _ => throw new ArgumentOutOfRangeException(nameof(entrypoint))
        };

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());
        Assert.Empty(appModel.Resources.OfType<PerlModuleInstallerResource>());

#pragma warning disable ASPIRECOMMAND001
        var annotation = Assert.Single(resource.Annotations.OfType<RequiredCommandAnnotation>());
#pragma warning restore ASPIRECOMMAND001
        Assert.Equal("perl", annotation.Command);
        Assert.Equal("https://www.perl.org/get.html", annotation.HelpLink);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WithPackage_RequiresSelectedCommandOnInstaller(bool useCpanMinus, bool packageFirst)
    {
        using var tempDir = new TempDirectory();
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", tempDir.Path, "app.pl");

        if (useCpanMinus && !packageFirst)
        {
            resource.WithCpanMinus();
        }

        resource.WithPackage("DBI");

        if (useCpanMinus && packageFirst)
        {
            resource.WithCpanMinus();
        }

        using var app = builder.Build();
        await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, resource.Resource.Name);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installer = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        var expectedCommand = useCpanMinus ? "cpanm" : "cpan";

#pragma warning disable ASPIRECOMMAND001
        Assert.DoesNotContain(resource.Resource.Annotations.OfType<RequiredCommandAnnotation>(), a => a.Command == "cpan");
        var annotation = Assert.Single(installer.Annotations.OfType<RequiredCommandAnnotation>());
        if (useCpanMinus)
        {
            Assert.DoesNotContain(
                appModel.Resources.SelectMany(r => r.Annotations.OfType<RequiredCommandAnnotation>()),
                a => a.Command == "cpan");
        }
#pragma warning restore ASPIRECOMMAND001
        Assert.Equal(expectedCommand, installer.Command);
        Assert.Equal(expectedCommand, annotation.Command);
        Assert.Equal(useCpanMinus ? "https://cpanmin.us/" : "https://metacpan.org/pod/CPAN", annotation.HelpLink);
        var wait = Assert.Single(resource.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(installer, wait.Resource);
        Assert.Equal(WaitType.WaitForCompletion, wait.WaitType);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WithProjectDependencies_RequiresSelectedCommandOnInstaller(bool useCarton, bool dependenciesFirst)
    {
        using var tempDir = new TempDirectory();
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", tempDir.Path, "app.pl");

        if (useCarton && !dependenciesFirst)
        {
            resource.WithCarton();
        }

        resource.WithProjectDependencies();

        if (useCarton && dependenciesFirst)
        {
            resource.WithCarton();
        }

        using var app = builder.Build();
        await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, resource.Resource.Name);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installer = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        var expectedCommand = useCarton ? "carton" : "cpanm";

#pragma warning disable ASPIRECOMMAND001
        Assert.DoesNotContain(
            appModel.Resources.SelectMany(r => r.Annotations.OfType<RequiredCommandAnnotation>()),
            a => a.Command == "cpan");
        var annotation = Assert.Single(installer.Annotations.OfType<RequiredCommandAnnotation>());
#pragma warning restore ASPIRECOMMAND001
        Assert.Equal(expectedCommand, installer.Command);
        Assert.Equal(expectedCommand, annotation.Command);
        Assert.Equal(useCarton ? "https://metacpan.org/pod/Carton" : "https://cpanmin.us/", annotation.HelpLink);
    }

    [Fact]
    public async Task AddPerlScript_WithCpanfile_RequiresCpanmWithoutCpan()
    {
        using var tempDir = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDir.Path, "cpanfile"), "requires 'DBI';");
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", tempDir.Path, "app.pl");

        using var app = builder.Build();
        await LoggingTestHelper.PublishBeforeStartAndCollectLogsAsync(builder, app, resource.Resource.Name);

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var installer = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());

#pragma warning disable ASPIRECOMMAND001
        Assert.DoesNotContain(
            appModel.Resources.SelectMany(r => r.Annotations.OfType<RequiredCommandAnnotation>()),
            a => a.Command == "cpan");
        var annotation = Assert.Single(installer.Annotations.OfType<RequiredCommandAnnotation>());
#pragma warning restore ASPIRECOMMAND001
        Assert.Equal("cpanm", installer.Command);
        Assert.Equal("cpanm", annotation.Command);
        Assert.Equal("https://cpanmin.us/", annotation.HelpLink);
    }

    [Fact]
    public void RequiredCommandAnnotationHasNoValidationCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

#pragma warning disable ASPIRECOMMAND001
        var annotation = resource.Annotations.OfType<RequiredCommandAnnotation>().Single(a => a.Command == "perl");
#pragma warning restore ASPIRECOMMAND001
        Assert.Null(annotation.ValidationCallback);
    }
}

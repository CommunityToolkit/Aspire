using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Services;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIRECOMMAND001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlInstallationManagerTests
{
    #region Registration and Required Commands

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

    #endregion

    #region Linux Positive Validation

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

    #endregion

    #region Negative Cases 

    [Fact]
    public async Task IsPerlInstalledAsync_ReturnsFalse_WhenPerlIsNotInstalled()
    {
        var manager = new PerlInstallationManager();

        var result = await manager.IsPerlInstalledAsync("/nonexistent/path/to/perl");

        Assert.False(result);
    }

    [Fact]
    public async Task IsPerlInstalledAsync_ReturnsFalse_WhenExecutableIsNotPerl()
    {
        var manager = new PerlInstallationManager();

        // "echo" exists but is not perl and won't output "This is perl"
        var result = await manager.IsPerlInstalledAsync("echo");

        Assert.False(result);
    }

    [Fact]
    public async Task IsPerlInstalledAsync_ThrowsWhenPathIsNullOrEmpty()
    {
        var manager = new PerlInstallationManager();

        await Assert.ThrowsAsync<ArgumentException>(() => manager.IsPerlInstalledAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.IsPerlInstalledAsync(null!));
    }

    [Fact]
    public async Task ValidationCallback_ReturnsFailure_WhenPerlIsNotInstalled()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var annotation = resource.Annotations.OfType<RequiredCommandAnnotation>().Single(a => a.Command == "perl");
        Assert.NotNull(annotation.ValidationCallback);

        // Use a nonexistent path to simulate missing perl
        var context = new RequiredCommandValidationContext("/nonexistent/perl", app.Services, CancellationToken.None);
        var result = await annotation.ValidationCallback(context);

        Assert.False(result.IsValid);
        Assert.Contains("not installed or not functional", result.ValidationMessage);
    }

    #endregion
}

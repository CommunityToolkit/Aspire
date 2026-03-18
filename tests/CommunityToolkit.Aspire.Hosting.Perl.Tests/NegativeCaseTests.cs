using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Perl.Services;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIRECOMMAND001

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class NegativeCaseTests
{
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
}

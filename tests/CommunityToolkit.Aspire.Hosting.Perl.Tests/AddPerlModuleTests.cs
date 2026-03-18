using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlModuleTests
{
    [Fact]
    public void AddPerlModuleCreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlModule("perl-worker", "lib", "MyApp::Worker");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("perl-worker", resource.Name);
    }

    [Fact]
    public void AddPerlModuleHasModuleEntrypointType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlModule("perl-worker", "lib", "MyApp::Worker");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlEntrypointAnnotation>(out var annotation));
        Assert.Equal(EntrypointType.Module, annotation.Type);
        Assert.Equal("MyApp::Worker", annotation.Entrypoint);
    }

    [Fact]
    public async Task AddPerlModuleSetsCorrectArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlModule("perl-worker", "lib", "MyApp::Worker");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<CommandLineArgsCallbackAnnotation>(out var argsAnnotation));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotation.Callback(context);

        Assert.Contains("-MMyApp::Worker", context.Args);
        Assert.Contains("-e", context.Args);
    }

    [Fact]
    public void AddPerlModuleSetsCommandToPerl()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlModule("perl-worker", "lib", "MyApp::Worker");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("perl", resource.Command);
    }

    [Fact]
    public void AddPerlModuleShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlModule("perl-worker", "lib", "MyApp::Worker"));
    }
}

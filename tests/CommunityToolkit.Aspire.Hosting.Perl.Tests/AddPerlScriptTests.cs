using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlScriptTests
{
    [Fact]
    public void AddPerlScriptCreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("perl-app", resource.Name);
    }

    [Fact]
    public void AddPerlScriptSetsCommandToPerl()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("perl", resource.Command);
    }

    [Fact]
    public async Task AddPerlScriptSetsCorrectArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<CommandLineArgsCallbackAnnotation>(out var argsAnnotation));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotation.Callback(context);

        Assert.Contains("-s", context.Args);
    }

    [Fact]
    public void AddPerlScriptSetsWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();
        var expectedWorkingDirectory = Path.GetFullPath("scripts", builder.AppHostDirectory);

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal(expectedWorkingDirectory, resource.WorkingDirectory);
    }

    [Fact]
    public void AddPerlScriptHasEntrypointAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlEntrypointAnnotation>(out var annotation));
        Assert.Equal(EntrypointType.Script, annotation.Type);
        Assert.Equal("app.pl", annotation.Entrypoint);
    }
}

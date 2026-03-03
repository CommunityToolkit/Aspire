using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class ResourceCreationTests
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

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("scripts", resource.WorkingDirectory);
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

    [Fact]
    public void AddPerlApiCreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("perl-api", resource.Name);
    }

    [Fact]
    public void AddPerlApiHasApiEntrypointType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlEntrypointAnnotation>(out var annotation));
        Assert.Equal(EntrypointType.API, annotation.Type);
        Assert.Equal("server.pl", annotation.Entrypoint);
    }

    [Fact]
    public async Task AddPerlApiSetsCorrectArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<CommandLineArgsCallbackAnnotation>(out var argsAnnotation));
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotation.Callback(context);

        // API args should be separate positional arguments (no -s flag)
        // so Mojolicious app->start reads "daemon" from @ARGV
        Assert.DoesNotContain("-s", context.Args);
        Assert.Contains("server.pl", context.Args);
        Assert.Contains("daemon", context.Args);
    }

    [Fact]
    public void AddPerlScriptImplementsIResourceWithServiceDiscovery()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.IsAssignableFrom<IResourceWithServiceDiscovery>(resource);
    }

    [Fact]
    public void AddPerlScriptIsExecutableResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.IsAssignableFrom<ExecutableResource>(resource);
    }

    [Fact]
    public void WithCpanmAddsPackageManagerAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanm("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlPackageManagerAnnotation>(out var annotation));
        Assert.Equal("cpanm", annotation.ExecutableName);
    }

    [Fact]
    public void WithCpanmAddsInstallCommandAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanm("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlModuleInstallCommandAnnotation>(out var annotation));
        Assert.NotNull(annotation.Args);
    }

    [Fact]
    public void WithCpanmCreatesInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl")
            .WithCpanm("Mojolicious");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var installerResource = Assert.Single(appModel.Resources.OfType<PerlModuleInstallerResource>());
        Assert.Contains("Mojolicious", installerResource.Name);
    }

    #region AddPerlModule

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

    #endregion

    #region AddPerlExecutable

    [Fact]
    public void AddPerlExecutableCreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("perl-bin", resource.Name);
    }

    [Fact]
    public void AddPerlExecutableHasExecutableEntrypointType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlEntrypointAnnotation>(out var annotation));
        Assert.Equal(EntrypointType.Executable, annotation.Type);
        Assert.Equal("my-compiled-perl", annotation.Entrypoint);
    }

    [Fact]
    public void AddPerlExecutableSetsCommandToExecutable()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("my-compiled-perl", resource.Command);
    }

    [Fact]
    public void AddPerlExecutableShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl"));
    }

    #endregion
}

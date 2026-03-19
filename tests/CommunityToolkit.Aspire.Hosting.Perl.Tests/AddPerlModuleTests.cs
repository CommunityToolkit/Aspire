using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlModuleTests
{
    [Theory]
    [InlineData("perl-worker", "lib", "MyApp::Worker")]
    [InlineData("queue-processor", "src", "Queue::Handler")]
    [InlineData("batch-runner", "modules", "Batch::Main")]
    public void AddPerlModule_ConfiguresResourceCorrectly(string name, string workingDir, string moduleName)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlModule(name, workingDir, moduleName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal(name, resource.Name);
        Assert.Equal("perl", resource.Command);

        var annotation = Assert.Single(resource.Annotations.OfType<PerlEntrypointAnnotation>());
        Assert.Equal(EntrypointType.Module, annotation.Type);
        Assert.Equal(moduleName, annotation.Entrypoint);
    }

    [Fact]
    public async Task AddPerlModuleSetsCorrectArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlModule("perl-worker", "lib", "MyApp::Worker");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var argsAnnotation = Assert.Single(resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>());
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotation.Callback(context);

        Assert.Contains("-MMyApp::Worker", context.Args);
        Assert.Contains("-e", context.Args);
    }

    [Fact]
    public void AddPerlModuleShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlModule("perl-worker", "lib", "MyApp::Worker"));
    }
}

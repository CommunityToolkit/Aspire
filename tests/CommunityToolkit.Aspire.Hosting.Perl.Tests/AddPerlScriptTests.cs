using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlScriptTests
{
    [Theory]
    [InlineData("perl-app", "scripts", "app.pl")]
    [InlineData("my-worker", "src", "worker.pl")]
    [InlineData("batch-job", "lib/tasks", "run.pl")]
    [InlineData("web-cgi", "cgi-bin", "handler.pl")]
    public void AddPerlScript_ConfiguresResourceCorrectly(string name, string workingDir, string entrypoint)
    {
        var builder = DistributedApplication.CreateBuilder();
        var expectedWorkingDirectory = Path.GetFullPath(workingDir, builder.AppHostDirectory);

        builder.AddPerlScript(name, workingDir, entrypoint);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal(name, resource.Name);
        Assert.Equal("perl", resource.Command);
        Assert.Equal(expectedWorkingDirectory, resource.WorkingDirectory);

        var annotation = Assert.Single(resource.Annotations.OfType<PerlEntrypointAnnotation>());
        Assert.Equal(EntrypointType.Script, annotation.Type);
        Assert.Equal(entrypoint, annotation.Entrypoint);
    }

    [Fact]
    public async Task AddPerlScriptSetsCorrectArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var argsAnnotation = Assert.Single(resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>());
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotation.Callback(context);

        Assert.Contains("-s", context.Args);
    }
}

using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlApiTests
{
    [Theory]
    [InlineData("perl-api", "api", "server.pl")]
    [InlineData("rest-service", "src/api", "app.pl")]
    public void AddPerlApi_ConfiguresResourceCorrectly(string name, string workingDir, string entrypoint)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi(name, workingDir, entrypoint);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal(name, resource.Name);
        Assert.Equal("perl", resource.Command);

        var annotation = Assert.Single(resource.Annotations.OfType<PerlEntrypointAnnotation>());
        Assert.Equal(EntrypointType.API, annotation.Type);
        Assert.Equal(entrypoint, annotation.Entrypoint);
    }

    [Fact]
    public async Task AddPerlApiSetsCorrectArgs()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        var argsAnnotation = Assert.Single(resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>());
        CommandLineArgsCallbackContext context = new([]);
        await argsAnnotation.Callback(context);

        // API args should be separate positional arguments (no -s flag)
        // so Mojolicious app->start reads "daemon" from @ARGV
        Assert.DoesNotContain("-s", context.Args);
        Assert.Contains("server.pl", context.Args);
        Assert.Contains("daemon", context.Args);
    }
}

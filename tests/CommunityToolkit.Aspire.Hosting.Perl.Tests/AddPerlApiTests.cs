using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlApiTests
{
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
}

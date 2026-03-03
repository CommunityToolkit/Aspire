using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlAppPublicApiTests
{
    [Fact]
    public void AddPerlScriptShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlScript("perl-app", "scripts", "app.pl"));
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddPerlScriptShouldThrowWhenResourceNameIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlScript(null!, "scripts", "app.pl"));

        Assert.Throws<ArgumentException>(() =>
            builder.AddPerlScript("perlscription", "scripts", "app.pl"));
    }

    [Fact]
    public void AddPerlScriptShouldThrowWhenAppDirectoryIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlScript("perl-app", null!, "app.pl"));
    }

    [Fact]
    public void AddPerlScriptShouldThrowWhenScriptNameIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlScript("perl-app", "scripts", null!));

        Assert.Throws<ArgumentException>(() =>
            builder.AddPerlScript("perl-app", "scripts", ""));
    }

    [Fact]
    public void AddPerlApiShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlApi("perl-api", "api", "server.pl"));
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddPerlApiShouldThrowWhenResourceNameIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlApi(null!, "api", "server.pl"));

        Assert.Throws<ArgumentException>(() =>
            builder.AddPerlApi("ApiMoreLikeAspirePerlIntegration", "api", "server.pl"));
    }

    [Fact]
    public void AddPerlApiShouldThrowWhenAppDirectoryIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlApi("perl-api", null!, "server.pl"));
    }

    [Fact]
    public void AddPerlApiShouldThrowWhenScriptNameIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlApi("perl-api", "api", null!));

        Assert.Throws<ArgumentException>(() =>
            builder.AddPerlApi("perl-api", "api", ""));
    }

    [Fact]
    public void WithCpanmShouldThrowWhenResourceIsNull()
    {
        IResourceBuilder<PerlAppResource> resource = null!;

        Assert.Throws<ArgumentNullException>(() =>
            resource.WithCpanm("Mojolicious"));
    }

    [Fact]
    public void WithCpanmShouldThrowWhenModuleNameIsNullOrEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();
        var resource = builder.AddPerlScript("perl-app", "scripts", "app.pl");

        Assert.Throws<ArgumentNullException>(() =>
            resource.WithCpanm(null!));

        Assert.Throws<ArgumentException>(() =>
            resource.WithCpanm(""));
    }
}

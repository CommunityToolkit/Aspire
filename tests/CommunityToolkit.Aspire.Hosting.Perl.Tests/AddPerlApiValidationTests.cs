using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlApiValidationTests
{
    [Fact]
    public void AddPerlApiShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlApi("perl-api", "api", "server.pl"));
        Assert.Equal("builder", exception.ParamName);
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
}

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlScriptValidationTests
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
}

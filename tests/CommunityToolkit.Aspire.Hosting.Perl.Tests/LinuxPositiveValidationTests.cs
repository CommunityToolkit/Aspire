using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class LinuxPositiveValidationTests
{
    [Fact]
    public void RequiredCommandAnnotation_HasCorrectShape()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

#pragma warning disable ASPIRECOMMAND001
        var annotation = resource.Annotations.OfType<RequiredCommandAnnotation>().Single(a => a.Command == "perl");
#pragma warning restore ASPIRECOMMAND001
        Assert.Equal("perl", annotation.Command);
        Assert.Equal("https://www.perl.org/get.html", annotation.HelpLink);
        Assert.Null(annotation.ValidationCallback);
    }
}

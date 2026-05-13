using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class NegativeCaseTests
{
    [Fact]
    public void RequiredCommandAnnotation_HasNoValidationCallback()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

#pragma warning disable ASPIRECOMMAND001
        var annotation = resource.Annotations.OfType<RequiredCommandAnnotation>().Single(a => a.Command == "perl");
#pragma warning restore ASPIRECOMMAND001
        Assert.Null(annotation.ValidationCallback);
    }
}

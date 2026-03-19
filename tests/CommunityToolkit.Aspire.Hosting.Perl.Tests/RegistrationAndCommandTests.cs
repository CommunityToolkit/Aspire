using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class RegistrationAndCommandTests
{
    [Fact]
    public void AddPerlScriptAddsRequiredCommandAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlScript("perl-app", "scripts", "app.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

#pragma warning disable ASPIRECOMMAND001
        var annotations = resource.Annotations.OfType<RequiredCommandAnnotation>().ToList();
#pragma warning restore ASPIRECOMMAND001
        Assert.Equal(2, annotations.Count);
        Assert.Contains(annotations, a => a.Command == "perl" && a.HelpLink == "https://www.perl.org/get.html");
        Assert.Contains(annotations, a => a.Command == "cpan" && a.HelpLink == "https://metacpan.org/pod/CPAN");
    }

    [Fact]
    public void AddPerlApiAddsRequiredCommandAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlApi("perl-api", "api", "server.pl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

#pragma warning disable ASPIRECOMMAND001
        var annotations = resource.Annotations.OfType<RequiredCommandAnnotation>().ToList();
#pragma warning restore ASPIRECOMMAND001
        Assert.Equal(2, annotations.Count);
        Assert.Contains(annotations, a => a.Command == "perl");
        Assert.Contains(annotations, a => a.Command == "cpan");
    }

    [Fact]
    public void RequiredCommandAnnotationHasNoValidationCallback()
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

using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlExecutableTests
{
    [Fact]
    public void AddPerlExecutableCreatesCorrectResourceType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("perl-bin", resource.Name);
    }

    [Fact]
    public void AddPerlExecutableHasExecutableEntrypointType()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.True(resource.TryGetLastAnnotation<PerlEntrypointAnnotation>(out var annotation));
        Assert.Equal(EntrypointType.Executable, annotation.Type);
        Assert.Equal("my-compiled-perl", annotation.Entrypoint);
    }

    [Fact]
    public void AddPerlExecutableSetsCommandToExecutable()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal("my-compiled-perl", resource.Command);
    }

    [Fact]
    public void AddPerlExecutableShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl"));
    }
}

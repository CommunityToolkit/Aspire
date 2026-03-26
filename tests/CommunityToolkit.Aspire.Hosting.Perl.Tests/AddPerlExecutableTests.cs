using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class AddPerlExecutableTests
{
    [Theory]
    [InlineData("perl-bin", "bin", "my-compiled-perl")]
    [InlineData("cli-tool", "dist", "run-tool")]
    [InlineData("daemon", "sbin", "perl-daemon")]
    public void AddPerlExecutable_ConfiguresResourceCorrectly(string name, string workingDir, string executable)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPerlExecutable(name, workingDir, executable);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PerlAppResource>());

        Assert.Equal(name, resource.Name);
        Assert.Equal(executable, resource.Command);

        var annotation = Assert.Single(resource.Annotations.OfType<PerlEntrypointAnnotation>());
        Assert.Equal(EntrypointType.Executable, annotation.Type);
        Assert.Equal(executable, annotation.Entrypoint);
    }

    [Fact]
    public void AddPerlExecutableShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddPerlExecutable("perl-bin", "bin", "my-compiled-perl"));
    }
}

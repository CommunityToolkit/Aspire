using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildContainerEntrypointTests
{
    [Fact]
    public void BuildContainerEntrypointArguments_Script_UsesPerlEntrypoint()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            localLibRoot: null);

        Assert.Equal(["perl", "app.pl"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Api_IncludesDaemonSubcommand()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.API,
            "app.pl",
            apiSubcommand: "daemon",
            localLibRoot: null);

        Assert.Equal(["perl", "app.pl", "daemon"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Module_UsesModuleRunShape()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            localLibRoot: null);

        Assert.Equal(["perl", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Executable_RunsDirectly()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Executable,
            "myapp",
            apiSubcommand: null,
            localLibRoot: "/app/vendor/perl5");

        Assert.Equal(["myapp"], args);
    }

    [Theory]
    [InlineData("/app/local")]
    [InlineData("/app/vendor/perl5")]
    [InlineData("/app/vendor lib/$perl")]
    public void BuildContainerEntrypointArguments_ModuleWithLocalLib_IncludesConfiguredPath(string localLibRoot)
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            localLibRoot);

        Assert.Equal(["perl", $"-I{localLibRoot}/lib/perl5", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

}

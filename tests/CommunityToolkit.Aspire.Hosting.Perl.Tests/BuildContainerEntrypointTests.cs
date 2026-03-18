using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildContainerEntrypointTests
{
#pragma warning disable ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
    [Fact]
    public void BuildContainerEntrypointArguments_Script_UsesPerlEntrypoint()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            useLocalLibPath: false);

        Assert.Equal(["perl", "app.pl"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Api_IncludesDaemonSubcommand()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.API,
            "app.pl",
            apiSubcommand: "daemon",
            useLocalLibPath: false);

        Assert.Equal(["perl", "app.pl", "daemon"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Module_UsesModuleRunShape()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            useLocalLibPath: false);

        Assert.Equal(["perl", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Executable_RunsDirectly()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Executable,
            "myapp",
            apiSubcommand: null,
            useLocalLibPath: false);

        Assert.Equal(["myapp"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_ModuleWithLocalLib_IncludesIncludePath()
    {
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            useLocalLibPath: true);

        Assert.Equal(["perl", "-Ilocal/lib/perl5", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

#pragma warning restore ASPIREDOCKERFILEBUILDER001, CTASPIREPERL002
}

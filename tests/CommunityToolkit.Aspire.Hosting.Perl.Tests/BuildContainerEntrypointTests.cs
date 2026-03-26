using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Perl.Annotations;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildContainerEntrypointTests
{
    [Fact]
    public void BuildContainerEntrypointArguments_Script_UsesPerlEntrypoint()
    {
#pragma warning disable CTASPIREPERL002
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Script,
            "app.pl",
            apiSubcommand: null,
            useLocalLibPath: false);
#pragma warning restore CTASPIREPERL002

        Assert.Equal(["perl", "app.pl"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Api_IncludesDaemonSubcommand()
    {
#pragma warning disable CTASPIREPERL002
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.API,
            "app.pl",
            apiSubcommand: "daemon",
            useLocalLibPath: false);
#pragma warning restore CTASPIREPERL002

        Assert.Equal(["perl", "app.pl", "daemon"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Module_UsesModuleRunShape()
    {
#pragma warning disable CTASPIREPERL002
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            useLocalLibPath: false);
#pragma warning restore CTASPIREPERL002

        Assert.Equal(["perl", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_Executable_RunsDirectly()
    {
#pragma warning disable CTASPIREPERL002
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Executable,
            "myapp",
            apiSubcommand: null,
            useLocalLibPath: false);
#pragma warning restore CTASPIREPERL002

        Assert.Equal(["myapp"], args);
    }

    [Fact]
    public void BuildContainerEntrypointArguments_ModuleWithLocalLib_IncludesIncludePath()
    {
#pragma warning disable CTASPIREPERL002
        var args = PerlAppResourceBuilderExtensions.BuildContainerEntrypointArguments(
            EntrypointType.Module,
            "MyApp::Worker",
            apiSubcommand: null,
            useLocalLibPath: true);
#pragma warning restore CTASPIREPERL002

        Assert.Equal(["perl", "-Ilocal/lib/perl5", "-MMyApp::Worker", "-e", "MyApp::Worker->run()"], args);
    }

}

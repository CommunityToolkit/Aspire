using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildInstallArgsTests
{
    [Fact]
    public void BuildInstallArgs_CpanmBasic()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: false, skipTest: false);

        Assert.Equal(["Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithForce()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: true, skipTest: false);

        Assert.Equal(["--force", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: false, skipTest: true);

        Assert.Equal(["--notest", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithForceAndNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpanm, "Mojolicious", force: true, skipTest: true);

        Assert.Equal(["--force", "--notest", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanBasic()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: false, skipTest: false);

        // cpan always needs -i to prevent interactive shell hangs
        Assert.Equal(["-i", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithForce()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: true, skipTest: false);

        // cpan requires -i when -f is used
        Assert.Equal(["-f", "-i", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: false, skipTest: true);

        Assert.Equal(["-T", "-i", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithForceAndNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Cpan, "DBI", force: true, skipTest: true);

        // cpan requires -i when -f is used
        Assert.Equal(["-f", "-T", "-i", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_UndefinedEnumValueThrows()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildInstallArgs((PerlPackageManager)99, "SomeModule", force: false, skipTest: false));
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithLocalLib()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpanm, "Mojolicious", force: false, skipTest: false, localLibPath: "/app/local");

        Assert.Equal(["--local-lib", "/app/local", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanmWithLocalLibAndForceAndNoTest()
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpanm, "Mojolicious", force: true, skipTest: true, localLibPath: "/app/local");

        Assert.Equal(["--local-lib", "/app/local", "--force", "--notest", "Mojolicious"], args);
    }

    [Fact]
    public void BuildInstallArgs_CpanWithLocalLib_DoesNotAddFlag()
    {
        // cpan does not support --local-lib; it relies on env vars instead
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpan, "DBI", force: false, skipTest: false, localLibPath: "/app/local");

        Assert.Equal(["-i", "DBI"], args);
    }

    [Fact]
    public void BuildInstallArgs_CartonThrowsForIndividualPackages()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Carton, "Mojolicious", force: false, skipTest: false));
    }
}

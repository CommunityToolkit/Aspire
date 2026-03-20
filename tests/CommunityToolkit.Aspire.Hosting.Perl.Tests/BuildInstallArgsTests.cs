using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildInstallArgsTests
{
    [Theory]
    [InlineData(false, false, new [] {"Mojolicious"})]
    [InlineData(true, false, new [] {"--force","Mojolicious"})]
    [InlineData(false, true, new [] {"--notest","Mojolicious"})]
    [InlineData(true, true, new [] {"--force","--notest","Mojolicious"})]
    public void BuildInstallArgs_CpanmFlags(bool force, bool skipTest, string[] expectedCsv)
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpanm, "Mojolicious", force, skipTest);

        Assert.Equal(expectedCsv, args);
    }

    [Theory]
    [InlineData(false, false, new[] { "-i", "DBI" })]
    [InlineData(true, false, new[] { "-f", "-i", "DBI" })]
    [InlineData(false, true, new[] { "-T", "-i", "DBI" })]
    [InlineData(true, true, new[] { "-f", "-T", "-i", "DBI" })]
    public void BuildInstallArgs_CpanFlags(bool force, bool skipTest, string[] expected)
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpan, "DBI", force, skipTest);

        Assert.Equal(expected, args);
    }

    [Theory]
    [InlineData(false, false, new[] { "--local-lib", "/app/local", "Mojolicious" })]
    [InlineData(true, true, new[] { "--local-lib", "/app/local", "--force", "--notest", "Mojolicious" })]
    public void BuildInstallArgs_CpanmWithLocalLib(bool force, bool skipTest, string[] expected)
    {
        var args = PerlAppResourceBuilderExtensions.BuildInstallArgs(
            PerlPackageManager.Cpanm, "Mojolicious", force, skipTest, localLibPath: "/app/local");

        Assert.Equal(expected, args);
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
    public void BuildInstallArgs_UndefinedEnumValueThrows()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildInstallArgs((PerlPackageManager)99, "SomeModule", force: false, skipTest: false));
    }

    [Fact]
    public void BuildInstallArgs_CartonThrowsForIndividualPackages()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildInstallArgs(PerlPackageManager.Carton, "Mojolicious", force: false, skipTest: false));
    }
}

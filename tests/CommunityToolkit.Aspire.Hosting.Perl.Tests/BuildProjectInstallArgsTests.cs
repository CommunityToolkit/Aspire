using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildProjectInstallArgsTests
{
    [Theory]
    [InlineData(PerlPackageManager.Cpanm, false, new[] { "--installdeps", "--notest", "." })]
    [InlineData(PerlPackageManager.Cpanm, true, new[] { "--installdeps", "--notest", "." })]
    [InlineData(PerlPackageManager.Carton, false, new[] { "install" })]
    [InlineData(PerlPackageManager.Carton, true, new[] { "install", "--deployment" })]
    public void BuildProjectInstallArgs_ValidManagers(PerlPackageManager manager, bool cartonDeployment, string[] expected)
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(manager, cartonDeployment);

        Assert.Equal(expected, args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Cpan_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Cpan, cartonDeployment: false));
    }

    [Fact]
    public void BuildProjectInstallArgs_CpanmWithLocalLib()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(
            PerlPackageManager.Cpanm, cartonDeployment: false, localLibPath: "/app/local");

        Assert.Equal(["--local-lib", "/app/local", "--installdeps", "--notest", "."], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_CartonWithLocalLib_DoesNotAddFlag()
    {
        // Carton manages its own local directory; --local-lib is not used
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(
            PerlPackageManager.Carton, cartonDeployment: false, localLibPath: "/app/local");

        Assert.Equal(["install"], args);
    }
}

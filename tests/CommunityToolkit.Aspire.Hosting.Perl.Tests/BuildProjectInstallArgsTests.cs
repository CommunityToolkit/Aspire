using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class BuildProjectInstallArgsTests
{
    [Fact]
    public void BuildProjectInstallArgs_Cpanm_ReturnsInstalldeps()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Cpanm, cartonDeployment: false);

        Assert.Equal(["--installdeps", "--notest", "."], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Cpanm_IgnoresDeploymentFlag()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Cpanm, cartonDeployment: true);

        // Deployment flag is only for Carton; cpanm ignores it
        Assert.Equal(["--installdeps", "--notest", "."], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Carton_ReturnsInstall()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Carton, cartonDeployment: false);

        Assert.Equal(["install"], args);
    }

    [Fact]
    public void BuildProjectInstallArgs_Carton_WithDeployment()
    {
        var args = PerlAppResourceBuilderExtensions.BuildProjectInstallArgs(PerlPackageManager.Carton, cartonDeployment: true);

        Assert.Equal(["install", "--deployment"], args);
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

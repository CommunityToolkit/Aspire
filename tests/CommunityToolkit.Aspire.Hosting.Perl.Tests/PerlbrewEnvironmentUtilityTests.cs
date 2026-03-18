using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlbrewEnvironmentUtilityTests
{
    [Fact, RequiresLinux]
    public void NormalizeVersion_PrefixesPerlWhenMissing()
    {
        var result = PerlbrewEnvironment.NormalizeVersion("5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact, RequiresLinux]
    public void NormalizeVersion_KeepsExistingPrefix()
    {
        var result = PerlbrewEnvironment.NormalizeVersion("perl-5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact, RequiresLinux]
    public void NormalizeVersion_NormalizesUpperCasePrefixToLower()
    {
        // Perlbrew installs under a lowercase directory name regardless of input casing.
        // NormalizeVersion must always emit a lowercase "perl-" prefix.
        var result = PerlbrewEnvironment.NormalizeVersion("Perl-5.38.0");

        Assert.Equal("perl-5.38.0", result);
    }

    [Fact, RequiresLinux]
    public void ResolvePerlbrewRoot_UsesExplicitValue()
    {
        var result = PerlbrewEnvironment.ResolvePerlbrewRoot("/custom/perlbrew");

        Assert.Equal("/custom/perlbrew", result);
    }

    [Fact, RequiresLinux]
    public void ResolvePerlbrewRoot_FallsBackToDefault()
    {
        // When no explicit root and no env var, falls back to ~/perl5/perlbrew
        var result = PerlbrewEnvironment.ResolvePerlbrewRoot(null);
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "perl5", "perlbrew");

        Assert.Equal(expected, result);
    }

    [Fact, RequiresLinux]
    public void GetExecutable_ReturnsCorrectPath()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var perlPath = env.GetExecutable("perl");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "perl");
        Assert.Equal(expected, perlPath);
    }

    [Fact, RequiresLinux]
    public void GetExecutable_ResolveCpanm()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var cpanmPath = env.GetExecutable("cpanm");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "cpanm");
        Assert.Equal(expected, cpanmPath);
    }

    [Fact, RequiresLinux]
    public void BinPath_IsCorrect()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin");
        Assert.Equal(expected, env.BinPath);
    }

    [Fact, RequiresLinux]
    public void VersionPath_IsCorrect()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var expected = Path.Combine("/home/user/perl5/perlbrew", "perls", "perl-5.38.0");
        Assert.Equal(expected, env.VersionPath);
    }

    [Fact, RequiresLinux]
    public void Properties_ReturnConstructorValues()
    {
        var env = new PerlbrewEnvironment("/opt/perlbrew", "perl-5.40.0");

        Assert.Equal("/opt/perlbrew", env.PerlbrewRoot);
        Assert.Equal("perl-5.40.0", env.Version);
    }
}

using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlbrewEnvironmentUtilityTests
{
    [Theory, RequiresLinux]
    [InlineData("5.38.0", "perl-5.38.0")]
    [InlineData("perl-5.38.0", "perl-5.38.0")]
    public void NormalizeVersion_NormalizesInput(string input, string expected)
    {
        var result = PerlbrewEnvironment.NormalizeVersion(input);

        Assert.Equal(expected, result);
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

    [Theory, RequiresLinux]
    [InlineData("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "perl")]
    [InlineData("/home/user/perl5/perlbrew", "perls", "perl-5.38.0", "bin", "cpanm")]
    public void GetExecutable_ReturnsCorrectBinPath(string root, string perlsDir, string version, string binDir, string executable)
    {
        var env = new PerlbrewEnvironment(root, version);

        var result = env.GetExecutable(executable);

        var expected = Path.Combine(root, perlsDir, version, binDir, executable);
        Assert.Equal(expected, result);
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

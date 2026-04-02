using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Services;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class DetectVersionFromPerlbrewTests
{
    [Fact]
    public void DetectVersionFromPerlbrew_ExtractsVersionFromEnvironment()
    {
        var env = new PerlbrewEnvironment("/home/user/perl5/perlbrew", "perl-5.38.0");

        var result = PerlVersionDetector.DetectVersionFromPerlbrew(env);

        Assert.Equal("5.38.0", result);
    }

    [Fact]
    public void DetectVersionFromPerlbrew_ThrowsWhenEnvironmentIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => PerlVersionDetector.DetectVersionFromPerlbrew(null!));
    }
}

using CommunityToolkit.Aspire.Hosting.Perl.Services;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class NormalizeVersionStringTests
{
    [Theory]
    [InlineData("v5.38.0", "5.38.0")]
    [InlineData("perl-5.38.0", "5.38.0")]
    [InlineData("5.38.0", "5.38.0")]
    [InlineData("V5.40.0", "5.40.0")]
    [InlineData("perl-5.40.0", "5.40.0")]
    [InlineData("  v5.38.0  ", "5.38.0")]
    public void NormalizeVersionString_HandlesVariousFormats(string input, string expected)
    {
        var result = PerlVersionDetector.NormalizeVersionString(input);

        Assert.Equal(expected, result);
    }
}

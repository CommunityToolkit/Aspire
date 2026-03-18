using CommunityToolkit.Aspire.Hosting.Perl.Services;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class DetectVersionFromCliTests
{
    [Fact, RequiresLinux]
    public async Task DetectVersionFromCli_ReturnsVersion_WhenPerlIsInstalled()
    {
        var result = await PerlVersionDetector.DetectVersionFromCliAsync("perl");

        Assert.NotNull(result);
        // Should be a numeric version like "5.38.0"
        Assert.Matches(@"^\d+\.\d+\.\d+$", result);
    }

    [Fact]
    public async Task DetectVersionFromCli_ReturnsNull_WhenExecutableDoesNotExist()
    {
        var result = await PerlVersionDetector.DetectVersionFromCliAsync("/nonexistent/perl");

        Assert.Null(result);
    }
}

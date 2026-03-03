using CommunityToolkit.Aspire.Hosting.Perl;
using CommunityToolkit.Aspire.Hosting.Perl.Services;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class PerlVersionDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public PerlVersionDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"perl-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region NormalizeVersionString

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

    #endregion

    #region DetectVersionFromFile

    [Fact]
    public void DetectVersionFromFile_ReturnsVersion_WhenFileExists()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".perl-version"), "5.38.0\n");

        var result = PerlVersionDetector.DetectVersionFromFile(_tempDir);

        Assert.Equal("5.38.0", result);
    }

    [Fact]
    public void DetectVersionFromFile_StripsPrefix_WhenFileContainsPerlPrefix()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".perl-version"), "perl-5.38.0");

        var result = PerlVersionDetector.DetectVersionFromFile(_tempDir);

        Assert.Equal("5.38.0", result);
    }

    [Fact]
    public void DetectVersionFromFile_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = PerlVersionDetector.DetectVersionFromFile(_tempDir);

        Assert.Null(result);
    }

    [Fact]
    public void DetectVersionFromFile_ReturnsNull_WhenFileIsEmpty()
    {
        File.WriteAllText(Path.Combine(_tempDir, ".perl-version"), "   ");

        var result = PerlVersionDetector.DetectVersionFromFile(_tempDir);

        Assert.Null(result);
    }

    [Fact]
    public void DetectVersionFromFile_ThrowsWhenDirectoryIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => PerlVersionDetector.DetectVersionFromFile(null!));
    }

    #endregion

    #region DetectVersionFromCli

    [LinuxOnlyFact]
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

    #endregion

    #region DetectVersionFromPerlbrew

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

    #endregion
}

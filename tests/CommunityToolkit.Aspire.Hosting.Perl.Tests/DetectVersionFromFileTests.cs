using CommunityToolkit.Aspire.Hosting.Perl.Services;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class DetectVersionFromFileTests : IDisposable
{
    private readonly string _tempDir;

    public DetectVersionFromFileTests()
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
}

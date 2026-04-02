using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class HasDependencyFilesTests : IDisposable
{
    private readonly TempDirectory _tempDir = new();

    public void Dispose() => _tempDir.Dispose();

    [Theory]
    [InlineData("cpanfile", "requires 'Mojolicious';", true)]
    [InlineData("Makefile.PL", "use ExtUtils::MakeMaker;", true)]
    [InlineData("Build.PL", "use Module::Build;", true)]
    [InlineData("app.pl", "true fakery;", false)]
    public void HasDependencyFiles_ReturnsTrueWhenDependencyFileExists(string fileName, string content, bool expectedResult)
    {
        File.WriteAllText(Path.Combine(_tempDir.Path, fileName), content);


        Assert.Equal(expectedResult, PerlAppResourceBuilderExtensions.HasDependencyFiles(_tempDir.Path, Directory.GetCurrentDirectory()));
    }

    
    [Fact]
    public void HasDependencyFiles_ResolvesRelativePath()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir.Path, "scripts"));
        File.WriteAllText(Path.Combine(subDir.FullName, "cpanfile"), "requires 'DBI';");

        var result = PerlAppResourceBuilderExtensions.HasDependencyFiles("scripts", _tempDir.Path);

        Assert.True(result);
    }

    [Fact]
    public void HasDependencyFiles_ReturnsFalseForEmptyDirectory()
    {
        var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(_tempDir.Path, Directory.GetCurrentDirectory());

        Assert.False(result);
    }
}

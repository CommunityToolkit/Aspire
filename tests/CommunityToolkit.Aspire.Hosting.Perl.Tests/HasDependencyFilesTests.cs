using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

public class HasDependencyFilesTests
{
    [Fact]
    public void HasDependencyFiles_ReturnsTrueWhenCpanfileExists()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "cpanfile"), "requires 'Mojolicious';");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsTrueWhenMakefilePLExists()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "Makefile.PL"), "use ExtUtils::MakeMaker;");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsTrueWhenBuildPLExists()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "Build.PL"), "use Module::Build;");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsFalseWhenNoDepFilesExist()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            File.WriteAllText(Path.Combine(tempDir.FullName, "app.pl"), "print 'hello';");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.False(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ResolvesRelativePath()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        var subDir = Directory.CreateDirectory(Path.Combine(tempDir.FullName, "scripts"));
        try
        {
            File.WriteAllText(Path.Combine(subDir.FullName, "cpanfile"), "requires 'DBI';");

            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles("scripts", tempDir.FullName);

            Assert.True(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void HasDependencyFiles_ReturnsFalseForEmptyDirectory()
    {
        var tempDir = Directory.CreateTempSubdirectory("perl-test-");
        try
        {
            var result = PerlAppResourceBuilderExtensions.HasDependencyFiles(tempDir.FullName, Directory.GetCurrentDirectory());

            Assert.False(result);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }
}

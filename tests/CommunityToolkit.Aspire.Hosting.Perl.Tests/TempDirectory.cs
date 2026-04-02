namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

/// <summary>
/// Disposable wrapper around <see cref="Directory.CreateTempSubdirectory"/>
/// for deterministic cleanup in tests.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("perl-test-");

    public string Path => _dir.FullName;

    public void Dispose() => _dir.Delete(recursive: true);
}

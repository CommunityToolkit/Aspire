using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Adminer.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        string repoRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        string appHostDirectory = Path.Combine(repoRoot, "examples", "adminer", "CommunityToolkit.Aspire.Hosting.Adminer.AppHost.Go");
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await ProcessTestUtilities.RunProcessAsync("aspire", ["restore", "--apphost", "apphost.go", "--non-interactive"], appHostDirectory, cancellationToken);
        await ProcessTestUtilities.RunProcessAsync("go", ["test", "./..."], appHostDirectory, cancellationToken);
    }
}

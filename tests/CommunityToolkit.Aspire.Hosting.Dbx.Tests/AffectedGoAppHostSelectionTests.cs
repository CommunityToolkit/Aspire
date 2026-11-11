using System.Diagnostics;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Dbx.Tests;

public class AffectedGoAppHostSelectionTests
{
    [Theory]
    [InlineData("one", "One.AppHost.Go", "apphost.go", "Hosting.One.Tests")]
    [InlineData("one", "One.AppHost.Go", "assets/config.json", "Hosting.One.Tests")]
    [InlineData("two", "Two.AppHost.Go", "apphost.go", "Hosting.Two.Tests")]
    public async Task GoAppHostChangesSelectTheirTestProject(string example, string appHost, string changedFile, string expectedTest)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var repositoryRoot = Path.GetFullPath(Path.Combine("..", "..", "..", "..", ".."));
        var selector = Path.Combine(repositoryRoot, "eng", "testing", "select-affected-tests.cs");
        var fixture = Path.Combine(Path.GetTempPath(), $"go-selection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixture);

        try
        {
            WriteFile(fixture, "tests/Directory.Build.props", "<Project />");
            WriteFile(fixture, "tests/CommunityToolkit.Aspire.Hosting.One.Tests/CommunityToolkit.Aspire.Hosting.One.Tests.csproj", "<Project />");
            WriteFile(fixture, "tests/CommunityToolkit.Aspire.Hosting.One.Tests/GoAppHostTests.cs",
                """GoAppHostTest.Run(appHostProject: "One.AppHost.Go", exampleName: "one");""");
            WriteFile(fixture, "tests/CommunityToolkit.Aspire.Hosting.Two.Tests/CommunityToolkit.Aspire.Hosting.Two.Tests.csproj", "<Project />");
            WriteFile(fixture, "tests/CommunityToolkit.Aspire.Hosting.Two.Tests/GoAppHostTests.cs",
                """const string Project = "Two.AppHost.Go"; const string Example = "two"; GoAppHostTest.Run(appHostProject: Project, exampleName: Example);""");
            WriteFile(fixture, "examples/one/One.AppHost.Go/apphost.go", "before");
            WriteFile(fixture, "examples/two/Two.AppHost.Go/apphost.go", "before");
            WriteFile(fixture, $"examples/{example}/{appHost}/{changedFile}", "before");

            await RunAsync("git", ["init", "--quiet"], fixture, cancellationToken);
            await RunAsync("git", ["add", "."], fixture, cancellationToken);
            await RunAsync("git", ["-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "base"], fixture, cancellationToken);
            WriteFile(fixture, $"examples/{example}/{appHost}/{changedFile}", "after");
            await RunAsync("git", ["add", "."], fixture, cancellationToken);
            await RunAsync("git", ["-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "--quiet", "-m", "change"], fixture, cancellationToken);

            var outputDirectory = Path.Combine(fixture, "output");
            var fixtureSelector = Path.Combine(fixture, "select-affected-tests.cs");
            File.Copy(selector, fixtureSelector);
            await RunAsync("dotnet", ["run", fixtureSelector, "--", "--repo-root", fixture, "--base-sha", "HEAD~1", "--head-sha", "HEAD", "--output-dir", outputDirectory], fixture, cancellationToken);
            using var selection = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputDirectory, "selection.json"), cancellationToken));
            Assert.False(selection.RootElement.GetProperty("runAll").GetBoolean());
            var selected = selection.RootElement.GetProperty("selectedTests").EnumerateArray().Select(static item => item.GetString()!).ToArray();
            Assert.Equal([expectedTest], selected);
            Assert.Contains(selection.RootElement.GetProperty("reasons").EnumerateArray(), static reason =>
                reason.GetString()!.Contains("Go app host test fixture", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(fixture, recursive: true);
        }
    }

    private static void WriteFile(string root, string path, string content)
    {
        var fullPath = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static async Task RunAsync(string executable, string[] arguments, string directory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment.Remove("GITHUB_OUTPUT");
        startInfo.Environment.Remove("GITHUB_STEP_SUMMARY");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        Assert.True(process.ExitCode == 0, $"{executable} exited with {process.ExitCode}: {await output}\n{await error}");
    }
}

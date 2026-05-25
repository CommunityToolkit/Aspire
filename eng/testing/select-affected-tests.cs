#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

var exitCode = await Program.MainAsync(args);
return exitCode;

static class Program
{
    private static readonly string[] GlobalFullRunPaths =
    [
        ".github/actions/",
        ".github/workflows/",
        "CommunityToolkit.Aspire.slnx",
        "Directory.Build.props",
        "Directory.Build.targets",
        "eng/testing/generate-test-list-for-workflow.sh",
        "eng/testing/select-affected-tests.cs",
        "global.json",
        "nuget.config",
        "tests/Directory.Build.props",
        "tests-app-hosts/Directory.Build.props",
    ];

    private static readonly HashSet<string> IgnoredSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
    };

    private const string TestInfraPropsPath = "tests/Directory.Build.props";

    public static async Task<int> MainAsync(string[] args)
    {
        var options = Options.Parse(args);
        var repoRoot = Path.GetFullPath(options.RepoRoot);
        var outputDir = Path.GetFullPath(options.OutputDir);
        Directory.CreateDirectory(outputDir);

        var allTests = LoadAllTests(repoRoot);
        var (projectRefs, packageRefs, projectPaths) = ProjectData(repoRoot);
        var nodeToTests = BuildNodeToTests(allTests, projectRefs);
        var packageToTests = BuildPackageToTests(packageRefs, nodeToTests);
        var testInfraPackages = LoadTestInfraPackages(repoRoot);

        var selected = new HashSet<string>(StringComparer.Ordinal);
        var ignoredFiles = new List<string>();
        var reasons = new List<string>();
        var runAll = false;
        var reason = "No affected tests were detected.";
        Payload payload;

        try
        {
            var diffFiles = await ChangedFilesAsync(repoRoot, options.BaseSha, options.HeadSha);

            foreach (var filePath in diffFiles)
            {
                var suffix = Path.GetExtension(filePath);
                if (IgnoredSuffixes.Contains(suffix))
                {
                    ignoredFiles.Add(filePath);
                    continue;
                }

                if (filePath == "Directory.Packages.props")
                {
                    var (packageIds, uncertain) = await PackageDiffAsync(repoRoot, options.BaseSha, options.HeadSha);
                    if (uncertain || packageIds.Count == 0)
                    {
                        runAll = true;
                        reason = "Fell back to the full test matrix because package impact could not be determined safely.";
                        reasons.Add(reason);
                        break;
                    }

                    if (packageIds.Overlaps(testInfraPackages))
                    {
                        runAll = true;
                        reason = "Fell back to the full test matrix because shared test infrastructure packages changed.";
                        reasons.Add(reason);
                        break;
                    }

                    var impacted = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var packageId in packageIds)
                    {
                        if (packageToTests.TryGetValue(packageId, out var tests))
                        {
                            impacted.UnionWith(tests);
                        }
                    }

                    var missing = packageIds
                        .Where(packageId => !packageToTests.ContainsKey(packageId))
                        .OrderBy(static packageId => packageId, StringComparer.Ordinal)
                        .ToList();

                    if (missing.Count > 0)
                    {
                        runAll = true;
                        reason = $"Fell back to the full test matrix because package usage for {string.Join(", ", missing)} could not be resolved safely.";
                        reasons.Add(reason);
                        break;
                    }

                    selected.UnionWith(impacted);
                    reasons.Add($"Selected {impacted.Count} tests from package changes in Directory.Packages.props: {string.Join(", ", packageIds.OrderBy(static packageId => packageId, StringComparer.Ordinal))}.");
                    continue;
                }

                if (MatchesGlobalFullRun(filePath))
                {
                    runAll = true;
                    reason = $"Fell back to the full test matrix because {filePath} is a global CI/build input.";
                    reasons.Add(reason);
                    break;
                }

                var project = NearestProject(filePath, projectPaths);
                if (project is not null)
                {
                    if (nodeToTests.TryGetValue(project, out var impacted))
                    {
                        selected.UnionWith(impacted);
                        if (impacted.Count > 0)
                        {
                            reasons.Add($"Selected {impacted.Count} tests because {filePath} belongs to {project}.");
                        }
                    }

                    continue;
                }

                if (filePath.StartsWith("src/", StringComparison.Ordinal) ||
                    filePath.StartsWith("tests/", StringComparison.Ordinal) ||
                    filePath.StartsWith("examples/", StringComparison.Ordinal) ||
                    filePath.StartsWith("tests-app-hosts/", StringComparison.Ordinal))
                {
                    runAll = true;
                    reason = $"Fell back to the full test matrix because {filePath} could not be mapped to a project safely.";
                    reasons.Add(reason);
                    break;
                }
            }

            var selectedTests = runAll
                ? allTests
                : allTests.Where(selected.Contains).ToList();

            if (!runAll && selectedTests.Count > 0)
            {
                reason = $"Selected {selectedTests.Count} affected tests from {diffFiles.Count} changed files.";
            }
            else if (!runAll && ignoredFiles.Count > 0 && selectedTests.Count == 0)
            {
                reason = "No tests were selected because only ignored documentation changes were detected.";
            }

            payload = new Payload(diffFiles, ignoredFiles, reason, reasons, runAll, selectedTests, null);
        }
        catch (GitProcessException exception)
        {
            var selectedTests = allTests;
            reason = "Fell back to the full test matrix because git diff failed.";
            reasons.Add(reason);
            payload = new Payload([], [], reason, reasons, true, selectedTests, exception.StandardError);
        }

        var selectionPath = Path.Combine(outputDir, "selection.json");
        await File.WriteAllTextAsync(
            selectionPath,
            JsonSerializer.Serialize(payload, SerializerOptions with { WriteIndented = true }));

        var changedFilesPath = Path.Combine(outputDir, "changed-files.txt");
        var changedFilesContent = payload.ChangedFiles.Count > 0
            ? string.Join('\n', payload.ChangedFiles) + "\n"
            : string.Empty;
        await File.WriteAllTextAsync(changedFilesPath, changedFilesContent);

        var summary = BuildSummary(payload);
        var summaryPath = Path.Combine(outputDir, "selection-summary.md");
        await WriteSummaryAsync(summaryPath, summary);
        await WriteOutputsAsync(payload.SelectedTests, payload.RunAll, payload.Reason);

        return 0;
    }

    private static List<string> LoadAllTests(string repoRoot)
    {
        var testsDir = Path.Combine(repoRoot, "tests");
        var hosting = Directory
            .EnumerateFiles(testsDir, "*Hosting.*.Tests.csproj", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(path => ToName(repoRoot, path));
        var client = Directory
            .EnumerateFiles(testsDir, "*.Tests.csproj", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Contains("Hosting.", StringComparison.Ordinal))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .Select(path => ToName(repoRoot, path));

        return [.. hosting, .. client];
    }

    private static string ToName(string repoRoot, string projectPath) =>
        Path.GetFileNameWithoutExtension(RelativePath(repoRoot, projectPath)).Replace("CommunityToolkit.Aspire.", string.Empty, StringComparison.Ordinal);

    private static (Dictionary<string, HashSet<string>> ProjectRefs, Dictionary<string, HashSet<string>> PackageRefs, List<string> ProjectPaths) ProjectData(string repoRoot)
    {
        var projectRefs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var packageRefs = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var projectPaths = new List<string>();

        foreach (var rootName in new[] { "src", "tests", "examples", "tests-app-hosts" })
        {
            var rootPath = Path.Combine(repoRoot, rootName);
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            foreach (var projectPath in Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.Ordinal))
            {
                var relative = RelativePath(repoRoot, projectPath);
                projectPaths.Add(relative);

                var refs = new HashSet<string>(StringComparer.Ordinal);
                var packages = new HashSet<string>(StringComparer.Ordinal);
                var document = XDocument.Load(projectPath);

                foreach (var element in document.Descendants())
                {
                    if (element.Name.LocalName == "ProjectReference")
                    {
                        var include = element.Attribute("Include")?.Value;
                        if (!string.IsNullOrEmpty(include))
                        {
                            var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, include.Replace('\\', Path.DirectorySeparatorChar)));
                            refs.Add(RelativePath(repoRoot, resolved));
                        }
                    }
                    else if (element.Name.LocalName == "PackageReference")
                    {
                        var include = element.Attribute("Include")?.Value;
                        if (!string.IsNullOrEmpty(include))
                        {
                            packages.Add(include);
                        }
                    }
                }

                projectRefs[relative] = refs;
                packageRefs[relative] = packages;
            }
        }

        return (projectRefs, packageRefs, projectPaths);
    }

    private static Dictionary<string, HashSet<string>> BuildNodeToTests(
        List<string> allTests,
        Dictionary<string, HashSet<string>> projectRefs)
    {
        var nodeToTests = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var knownTests = new HashSet<string>(allTests, StringComparer.Ordinal);

        foreach (var (project, _) in projectRefs)
        {
            if (!project.StartsWith("tests/", StringComparison.Ordinal))
            {
                continue;
            }

            var testName = Path.GetFileNameWithoutExtension(project).Replace("CommunityToolkit.Aspire.", string.Empty, StringComparison.Ordinal);
            if (!knownTests.Contains(testName))
            {
                continue;
            }

            var queue = new Stack<string>();
            queue.Push(project);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            while (queue.Count > 0)
            {
                var current = queue.Pop();
                if (!seen.Add(current))
                {
                    continue;
                }

                if (!nodeToTests.TryGetValue(current, out var impacted))
                {
                    impacted = new HashSet<string>(StringComparer.Ordinal);
                    nodeToTests[current] = impacted;
                }

                impacted.Add(testName);

                if (projectRefs.TryGetValue(current, out var refs))
                {
                    foreach (var reference in refs)
                    {
                        queue.Push(reference);
                    }
                }
            }
        }

        return nodeToTests;
    }

    private static Dictionary<string, HashSet<string>> BuildPackageToTests(
        Dictionary<string, HashSet<string>> packageRefs,
        Dictionary<string, HashSet<string>> nodeToTests)
    {
        var packageToTests = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var (project, packages) in packageRefs)
        {
            if (!nodeToTests.TryGetValue(project, out var impacted))
            {
                continue;
            }

            foreach (var package in packages)
            {
                if (!packageToTests.TryGetValue(package, out var tests))
                {
                    tests = new HashSet<string>(StringComparer.Ordinal);
                    packageToTests[package] = tests;
                }

                tests.UnionWith(impacted);
            }
        }

        return packageToTests;
    }

    private static HashSet<string> LoadTestInfraPackages(string repoRoot)
    {
        var packages = new HashSet<string>(StringComparer.Ordinal);
        var document = XDocument.Load(Path.Combine(repoRoot, TestInfraPropsPath));

        foreach (var element in document.Descendants().Where(static element => element.Name.LocalName == "PackageReference"))
        {
            var include = element.Attribute("Include")?.Value;
            if (!string.IsNullOrEmpty(include))
            {
                packages.Add(include);
            }
        }

        return packages;
    }

    private static async Task<List<string>> ChangedFilesAsync(string repoRoot, string baseSha, string headSha)
    {
        var output = await RunGitAsync(repoRoot, "diff", "--name-only", $"{baseSha}...{headSha}");
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static async Task<(HashSet<string> PackageIds, bool Uncertain)> PackageDiffAsync(string repoRoot, string baseSha, string headSha)
    {
        var output = await RunGitAsync(repoRoot, "diff", "--unified=0", $"{baseSha}...{headSha}", "--", "Directory.Packages.props");

        var packageIds = new HashSet<string>(StringComparer.Ordinal);
        var uncertain = false;

        foreach (var rawLine in output.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrEmpty(rawLine) ||
                rawLine.StartsWith("diff --git", StringComparison.Ordinal) ||
                rawLine.StartsWith("index ", StringComparison.Ordinal) ||
                rawLine.StartsWith("--- ", StringComparison.Ordinal) ||
                rawLine.StartsWith("+++ ", StringComparison.Ordinal) ||
                rawLine.StartsWith("@@", StringComparison.Ordinal))
            {
                continue;
            }

            if (rawLine[0] is not ('+' or '-'))
            {
                continue;
            }

            var line = rawLine[1..].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("<!--", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains("PackageVersion", StringComparison.Ordinal) &&
                TryGetAttributeValue(line, "Include", out var include))
            {
                packageIds.Add(include);
                continue;
            }

            uncertain = true;
        }

        return (packageIds, uncertain);
    }

    private static bool TryGetAttributeValue(string line, string attributeName, out string value)
    {
        var marker = $"{attributeName}=\"";
        var start = line.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            value = string.Empty;
            return false;
        }

        start += marker.Length;
        var end = line.IndexOf('"', start);
        if (end < 0)
        {
            value = string.Empty;
            return false;
        }

        value = line[start..end];
        return true;
    }

    private static string? NearestProject(string filePath, List<string> projectPaths)
    {
        string? bestMatch = null;
        var bestLength = -1;

        foreach (var project in projectPaths)
        {
            var projectDir = NormalizeDirectory(Path.GetDirectoryName(project));
            var prefix = string.IsNullOrEmpty(projectDir) ? string.Empty : $"{projectDir}/";
            if (filePath != project && !filePath.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (projectDir.Length > bestLength)
            {
                bestLength = projectDir.Length;
                bestMatch = project;
            }
        }

        return bestMatch;
    }

    private static string NormalizeDirectory(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');

    private static bool MatchesGlobalFullRun(string path) =>
        GlobalFullRunPaths.Any(entry => path == entry || path.StartsWith(entry, StringComparison.Ordinal));

    private static async Task WriteOutputsAsync(IReadOnlyList<string> selectedTests, bool runAll, string reason)
    {
        var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (string.IsNullOrEmpty(githubOutput))
        {
            return;
        }

        var lines = new[]
        {
            $"selected_tests={JsonSerializer.Serialize(selectedTests, SerializerOptions)}",
            $"has_tests={(selectedTests.Count > 0 ? "true" : "false")}",
            $"run_all={(runAll ? "true" : "false")}",
            $"reason={reason}",
        };

        await File.AppendAllLinesAsync(githubOutput, lines);
    }

    private static async Task WriteSummaryAsync(string summaryPath, string summary)
    {
        await File.WriteAllTextAsync(summaryPath, summary);

        var stepSummary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrEmpty(stepSummary))
        {
            return;
        }

        await File.AppendAllTextAsync(stepSummary, summary);
    }

    private static string BuildSummary(Payload payload)
    {
        var lines = new List<string>
        {
            "## Affected test selection",
            string.Empty,
            $"- Mode: {(payload.RunAll ? "full" : "selective")}",
            $"- Reason: {payload.Reason}",
            $"- Selected tests: {payload.SelectedTests.Count}",
            string.Empty,
            "### Changed files",
        };

        if (payload.ChangedFiles.Count > 0)
        {
            lines.AddRange(payload.ChangedFiles.Select(filePath => $"- `{filePath}`"));
        }
        else
        {
            lines.Add("- None");
        }

        if (payload.IgnoredFiles.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("### Ignored files");
            lines.AddRange(payload.IgnoredFiles.Select(filePath => $"- `{filePath}`"));
        }

        if (payload.SelectedTests.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("### Selected tests");
            lines.AddRange(payload.SelectedTests.Select(testName => $"- `{testName}`"));
        }

        if (payload.Reasons.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("### Selection details");
            lines.AddRange(payload.Reasons.Select(entry => $"- {entry}"));
        }

        return string.Join('\n', lines) + "\n";
    }

    private static async Task<string> RunGitAsync(string repoRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var standardOutput = await stdoutTask;
        var standardError = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new GitProcessException(standardError);
        }

        return standardOutput.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string RelativePath(string repoRoot, string path) =>
        Path.GetRelativePath(repoRoot, path).Replace('\\', '/');

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record Options(string RepoRoot, string BaseSha, string HeadSha, string OutputDir)
    {
        public static Options Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var index = 0; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Expected --repo-root, --base-sha, --head-sha, and --output-dir arguments.");
                }

                values[args[index][2..]] = args[index + 1];
            }

            return new Options(
                GetRequired(values, "repo-root"),
                GetRequired(values, "base-sha"),
                GetRequired(values, "head-sha"),
                GetRequired(values, "output-dir"));
        }

        private static string GetRequired(Dictionary<string, string> values, string name) =>
            values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"Missing required argument --{name}.");
    }

    private sealed record Payload(
        [property: JsonPropertyName("changedFiles")] IReadOnlyList<string> ChangedFiles,
        [property: JsonPropertyName("ignoredFiles")] IReadOnlyList<string> IgnoredFiles,
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons,
        [property: JsonPropertyName("runAll")] bool RunAll,
        [property: JsonPropertyName("selectedTests")] IReadOnlyList<string> SelectedTests,
        [property: JsonPropertyName("stderr")] string? StandardError);

    private sealed class GitProcessException(string standardError) : Exception
    {
        public string StandardError { get; } = standardError;
    }
}

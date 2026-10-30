using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// A chart directory on the host and the local charts it depends on through <c>file://</c>
/// repositories, laid out in the Helm container as they are laid out on the host so that each
/// relative <c>file://</c> path still resolves.
/// </summary>
internal sealed class HelmLocalChart
{
    /// <summary>Where the chart directories are mounted read-only.</summary>
    internal const string MountPath = "/helm-chart-source";

    /// <summary>
    /// Where the install script copies <see cref="MountPath"/>: <c>helm dependency build</c>
    /// writes <c>charts/</c> beside <c>Chart.yaml</c>, so nothing is written into the source tree.
    /// </summary>
    internal const string WorkPath = "/tmp/helm-chart";

    private HelmLocalChart(string directory) => Directory = directory;

    /// <summary>The absolute host path of the chart directory.</summary>
    public string Directory { get; }

    /// <summary>
    /// Host directories to mount read-only, each with its path under <see cref="MountPath"/>:
    /// the chart, and every local dependency outside it.
    /// </summary>
    public List<(string Source, string Target)> Mounts { get; } = [];

    /// <summary>The path of the chart once copied to <see cref="WorkPath"/>.</summary>
    public string ChartPath { get; private set; } = WorkPath;

    /// <summary>
    /// The copied paths of the chart and its local dependencies, dependencies first: each is
    /// built before the chart that packages it, so it is packaged with its own dependencies.
    /// </summary>
    public List<string> BuildPaths { get; } = [];

    /// <summary>
    /// The HTTP repositories the charts' dependencies come from. Helm resolves a dependency only
    /// from a repository it knows, so each is added before the dependencies are built.
    /// </summary>
    public SortedSet<string> Repositories { get; } = new(StringComparer.Ordinal);

    /// <summary>Reads the dependencies of the chart in <paramref name="directory"/>, and of its local dependencies.</summary>
    /// <exception cref="InvalidOperationException">A local dependency shares no directory with the chart.</exception>
    public static HelmLocalChart Load(string directory)
    {
        var chart = new HelmLocalChart(directory);

        // Depth first, each chart after its dependencies.
        var charts = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Visit(Normalize(directory));

        // The deepest directory holding every chart becomes MountPath, so the charts keep their
        // places relative to each other.
        var root = charts[^1];
        foreach (var other in charts)
        {
            while (!IsWithin(other, root))
            {
                root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException(
                    $"The chart '{directory}' depends on '{other}', which shares no directory with it, so the two cannot be mounted side by side.");
            }
        }

        // One mount per directory not already inside another mounted one.
        foreach (var path in charts.OrderBy(p => p.Length))
        {
            if (!chart.Mounts.Any(m => IsWithin(path, m.Source)))
                chart.Mounts.Add((path, ContainerPath(MountPath, root, path)));
        }

        chart.ChartPath = ContainerPath(WorkPath, root, charts[^1]);
        chart.BuildPaths.AddRange(charts.Select(path => ContainerPath(WorkPath, root, path)));

        return chart;

        void Visit(string path)
        {
            if (!visited.Add(path))
                return;

            foreach (var repository in ReadDependencyRepositories(path))
            {
                if (repository.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || repository.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    chart.Repositories.Add(repository);
                }
                else if (repository.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    // A missing directory is left for helm dependency build to report.
                    var dependency = Normalize(Path.Combine(path, repository["file://".Length..]));
                    if (File.Exists(Path.Combine(dependency, "Chart.yaml")))
                        Visit(dependency);
                }

                // OCI references, and repository names Helm resolves itself, need nothing added.
            }

            charts.Add(path);
        }
    }

    /// <summary>
    /// The <c>repository</c> of every dependency the chart in <paramref name="directory"/> lists,
    /// in <c>Chart.yaml</c> or, for an <c>apiVersion: v1</c> chart, in <c>requirements.yaml</c>.
    /// </summary>
    private static IEnumerable<string> ReadDependencyRepositories(string directory)
    {
        foreach (var file in new[] { "Chart.yaml", "requirements.yaml" })
        {
            var path = Path.Combine(directory, file);
            if (!File.Exists(path))
                continue;

            var yaml = new YamlStream();
            try
            {
                using var reader = File.OpenText(path);
                yaml.Load(reader);
            }
            catch (YamlException)
            {
                // Helm reports the error, in the release's logs, when it loads the chart.
                continue;
            }

            if (yaml.Documents is not [{ RootNode: YamlMappingNode document }, ..]
                || ValueOf(document, "dependencies") is not YamlSequenceNode dependencies)
            {
                continue;
            }

            foreach (var dependency in dependencies.OfType<YamlMappingNode>())
            {
                if (ValueOf(dependency, "repository") is YamlScalarNode { Value: { } repository }
                    && !string.IsNullOrWhiteSpace(repository))
                {
                    yield return repository.Trim();
                }
            }
        }
    }

    private static YamlNode? ValueOf(YamlMappingNode mapping, string key) =>
        mapping.Children.FirstOrDefault(entry => entry.Key is YamlScalarNode { Value: var name } && name == key).Value;

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsWithin(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative == "."
            || (relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relative));
    }

    /// <summary>The container path, under <paramref name="basePath"/>, of host <paramref name="path"/> within <paramref name="root"/>.</summary>
    private static string ContainerPath(string basePath, string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "." ? basePath : $"{basePath}/{relative.Replace(Path.DirectorySeparatorChar, '/')}";
    }
}

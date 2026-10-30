using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class HelmReleaseValuesAndChartsTests : IDisposable
{
    private readonly string _chartDirectory = Directory.CreateTempSubdirectory("aspire-k3s-chart").FullName;
    private readonly string _chartsRoot = Directory.CreateTempSubdirectory("aspire-k3s-charts").FullName;

    public HelmReleaseValuesAndChartsTests()
    {
        File.WriteAllText(Path.Combine(_chartDirectory, "Chart.yaml"), """
            apiVersion: v2
            name: local
            version: 0.1.0
            dependencies:
              - name: redis-ha
                version: 4.39.0
                repository: "https://dandydeveloper.github.io/charts"
            """);
    }

    public void Dispose()
    {
        Directory.Delete(_chartDirectory, recursive: true);
        Directory.Delete(_chartsRoot, recursive: true);
    }

    private static HelmReleaseResource Release(IDistributedApplicationBuilder appBuilder) =>
        appBuilder.Resources.OfType<HelmReleaseResource>().Single();

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationBuilder appBuilder)
    {
        var environment = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(appBuilder.ExecutionContext, environment);
        foreach (var annotation in Release(appBuilder).Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context);
        return environment;
    }

    /// <summary>Writes <paramref name="chartYaml"/> as the Chart.yaml of <paramref name="name"/> under the charts root.</summary>
    private string Chart(string name, string chartYaml)
    {
        var directory = Path.Combine(_chartsRoot, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Chart.yaml"), chartYaml);
        return directory;
    }

    // ── Values resolved when the release runs ─────────────────────────────────

    [Fact]
    public void AParameterValueTravelsInTheEnvironmentNotTheScript()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = appBuilder.AddParameter("password", "s3cr,et", secret: true);
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("auth.password", password);

        var release = Release(appBuilder);
        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        Assert.Equal("ASPIRE_HELM_VALUE_0", release.HelmValueVariables["auth.password"].Name);
        Assert.DoesNotContain("s3cr", script);
        Assert.Contains("_aspire_helm_value_0=$(printf '%s.' \"$ASPIRE_HELM_VALUE_0\" | sed -e 's/[\\\\,{}]/\\\\&/g')", script);
        Assert.Contains("--set 'auth.password='\"$_aspire_helm_value_0\"", script);
    }

    [Fact]
    public void AResolvedValueKeepsItsTrailingNewlines()
    {
        // Command substitution strips trailing newlines, so the escaped value carries a '.' that is
        // removed once the substitution is done.
        var appBuilder = DistributedApplication.CreateBuilder();
        var certificate = appBuilder.AddParameter("certificate", "-----END CERTIFICATE-----\n");
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("tls.crt", certificate);

        var script = K3sHelmBuilderExtensions.BuildHelmScript(Release(appBuilder));

        Assert.Contains("_aspire_helm_value_0=${_aspire_helm_value_0%.}\n", script);
        Assert.True(
            script.IndexOf("_aspire_helm_value_0=${_aspire_helm_value_0%.}", StringComparison.Ordinal)
            < script.IndexOf("helm upgrade --install", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AParameterValueIsSetAsAnEnvironmentVariable()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = appBuilder.AddParameter("password", "s3cret", secret: true);
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("auth.password", password);

        var environment = await EnvironmentAsync(appBuilder);

        var value = Assert.IsType<ReferenceExpression>(environment["ASPIRE_HELM_VALUE_0"]);
        Assert.Equal("s3cret", await value.GetValueAsync(CancellationToken.None));
    }

    [Fact]
    public void AParameterValueRelatesTheReleaseToTheParameter()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = appBuilder.AddParameter("password", "s3cret", secret: true);
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("auth.password", password);

        Assert.Contains(
            Release(appBuilder).Annotations.OfType<ResourceRelationshipAnnotation>(),
            r => ReferenceEquals(r.Resource, password.Resource));
    }

    [Fact]
    public async Task SettingAKeyAgainKeepsItsVariable()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var first = appBuilder.AddParameter("first", "1");
        var second = appBuilder.AddParameter("second", "2");
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("key", first)
            .WithHelmValue("key", second);

        var release = Release(appBuilder);
        var environment = await EnvironmentAsync(appBuilder);

        Assert.Equal("ASPIRE_HELM_VALUE_0", Assert.Single(release.HelmValueVariables).Value.Name);
        var value = Assert.IsType<ReferenceExpression>(Assert.Single(environment, e => e.Key.StartsWith("ASPIRE_HELM_VALUE_", StringComparison.Ordinal)).Value);
        Assert.Equal("2", await value.GetValueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ALiteralValueReplacesAnEarlierExpressionForTheSameKey()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var parameter = appBuilder.AddParameter("p", "resolved");
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("key", parameter)
            .WithHelmValue("key", "literal");

        var release = Release(appBuilder);
        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);
        var environment = await EnvironmentAsync(appBuilder);

        Assert.Empty(release.HelmValueVariables);
        Assert.Contains("--set 'key=literal'", script);
        Assert.DoesNotContain("_aspire_helm_value_", script);
        // The replaced value no longer reaches the container, where it would sit unused.
        Assert.DoesNotContain(environment.Keys, k => k.StartsWith("ASPIRE_HELM_VALUE_", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExpressionReplacesAnEarlierLiteralForTheSameKey()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var parameter = appBuilder.AddParameter("p", "resolved");
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("key", "literal")
            .WithHelmValue("key", ReferenceExpression.Create($"{parameter.Resource}"));

        var release = Release(appBuilder);

        Assert.Empty(release.HelmValues);
        Assert.Contains("key", release.HelmValueVariables.Keys);
    }

    [Fact]
    public void ResolvedValuesFollowLiteralValues()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var parameter = appBuilder.AddParameter("p", "resolved");
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValuesFile("/tmp/values.yaml")
            .WithHelmValue("resolved", parameter)
            .WithHelmValue("literal", "x");

        var script = K3sHelmBuilderExtensions.BuildHelmScript(Release(appBuilder));

        Assert.True(
            script.IndexOf("--values", StringComparison.Ordinal) < script.IndexOf("--set 'literal=x'", StringComparison.Ordinal)
            && script.IndexOf("--set 'literal=x'", StringComparison.Ordinal) < script.IndexOf("--set 'resolved='", StringComparison.Ordinal));
    }

    [Fact]
    public void WithHelmValueShouldThrowWhenParameterIsNull()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var release = appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app");

        Assert.Throws<ArgumentNullException>(() => release.WithHelmValue("key", (IResourceBuilder<ParameterResource>)null!));
    }

    // ── Chart directories ─────────────────────────────────────────────────────

    [Fact]
    public void AChartDirectoryIsMountedReadOnly()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", _chartDirectory);

        var release = Release(appBuilder);

        Assert.Equal(_chartDirectory, release.LocalChart?.Directory);
        var mount = Assert.Single(release.Annotations.OfType<ContainerMountAnnotation>(), m => m.Target == "/helm-chart-source");
        Assert.Equal(_chartDirectory, mount.Source);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public void AChartDirectoryIsCopiedAndItsDependenciesBuilt()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", _chartDirectory);

        var script = K3sHelmBuilderExtensions.BuildHelmScript(Release(appBuilder));

        Assert.Contains("cp -R /helm-chart-source /tmp/helm-chart", script);
        Assert.Contains("helm repo add --force-update 'aspire-k3s-dependency-0' 'https://dandydeveloper.github.io/charts' > /dev/null", script);
        Assert.Contains("helm dependency build '/tmp/helm-chart'", script);
        Assert.Contains("helm upgrade --install 'app' '/tmp/helm-chart'", script);
        Assert.DoesNotContain("--version", script);
    }

    [Fact]
    public void DependencyRepositoriesAreFoundWhateverTheOrderOfTheirKeys()
    {
        var chart = Chart("ordered", """
            apiVersion: v2
            name: ordered
            version: 0.1.0
            annotations:
              repository: https://not-a-dependency.example.com
            dependencies:
              - repository: https://first-key.example.com
                name: first
                version: 1.0.0
              - name: second
                version: 1.0.0
                repository: 'https://last-key.example.com'
              - { name: flow, version: 1.0.0, repository: "https://flow.example.com" }
              - name: oci
                version: 1.0.0
                repository: oci://registry.example.com/charts
            """);
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", chart);

        var release = Release(appBuilder);

        Assert.Equal(
            ["https://first-key.example.com", "https://flow.example.com", "https://last-key.example.com"],
            release.LocalChart!.Repositories);
    }

    [Fact]
    public void DependenciesAreBuiltWhateverFormTheChartTakes()
    {
        // JSON is YAML too. Helm, not a pattern, decides whether the chart has dependencies.
        var chart = Chart("json", """
            {"apiVersion": "v2", "name": "json", "version": "0.1.0",
             "dependencies": [{"name": "redis", "version": "1.0.0", "repository": "https://charts.example.com"}]}
            """);
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", chart);

        var script = K3sHelmBuilderExtensions.BuildHelmScript(Release(appBuilder));

        Assert.Contains("helm repo add --force-update 'aspire-k3s-dependency-0' 'https://charts.example.com' > /dev/null", script);
        Assert.Contains("helm dependency build '/tmp/helm-chart'", script);
    }

    [Fact]
    public void AnApiVersionV1ChartsRequirementsAreRead()
    {
        var chart = Chart("v1", """
            apiVersion: v1
            name: v1
            version: 0.1.0
            """);
        File.WriteAllText(Path.Combine(chart, "requirements.yaml"), """
            dependencies:
              - name: redis
                version: 1.0.0
                repository: https://charts.example.com
            """);
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", chart);

        var script = K3sHelmBuilderExtensions.BuildHelmScript(Release(appBuilder));

        Assert.Contains("helm repo add --force-update 'aspire-k3s-dependency-0' 'https://charts.example.com' > /dev/null", script);
        Assert.Contains("helm dependency build '/tmp/helm-chart'", script);
    }

    [Fact]
    public void AnInvalidChartIsLeftForHelmToReport()
    {
        var chart = Chart("invalid", "dependencies: [");
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", chart);

        var release = Release(appBuilder);

        Assert.Empty(release.LocalChart!.Repositories);
        Assert.Contains("helm dependency build '/tmp/helm-chart'", K3sHelmBuilderExtensions.BuildHelmScript(release));
    }

    [Fact]
    public void ASiblingChartDependencyIsMountedBesideTheChart()
    {
        var app = Chart("app", """
            apiVersion: v2
            name: app
            version: 0.1.0
            dependencies:
              - name: common
                version: 0.1.0
                repository: file://../common
            """);
        var common = Chart("common", """
            apiVersion: v2
            name: common
            version: 0.1.0
            dependencies:
              - name: redis
                version: 1.0.0
                repository: https://charts.example.com
            """);
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", app);

        var release = Release(appBuilder);
        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);
        var mounts = release.Annotations.OfType<ContainerMountAnnotation>()
            .Where(m => m.Target.StartsWith("/helm-chart-source", StringComparison.Ordinal))
            .ToDictionary(m => m.Target, m => m.Source);

        Assert.Equal(2, mounts.Count);
        Assert.Equal(app, mounts["/helm-chart-source/app"]);
        Assert.Equal(common, mounts["/helm-chart-source/common"]);
        Assert.Contains("helm repo add --force-update 'aspire-k3s-dependency-0' 'https://charts.example.com' > /dev/null", script);
        // The dependency is built first, so the chart packages it with its own dependencies.
        var buildCommon = script.IndexOf("helm dependency build '/tmp/helm-chart/common'", StringComparison.Ordinal);
        var buildApp = script.IndexOf("helm dependency build '/tmp/helm-chart/app'", StringComparison.Ordinal);
        Assert.True(buildCommon >= 0 && buildCommon < buildApp);
        Assert.Contains("helm upgrade --install 'app' '/tmp/helm-chart/app'", script);
    }

    [Fact]
    public void ALocalDependencyInsideTheChartIsNotMountedAgain()
    {
        var app = Chart("nested", """
            apiVersion: v2
            name: nested
            version: 0.1.0
            dependencies:
              - name: library
                version: 0.1.0
                repository: file://./libraries/library
            """);
        Chart(Path.Combine("nested", "libraries", "library"), """
            apiVersion: v2
            name: library
            version: 0.1.0
            """);
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", app);

        var release = Release(appBuilder);

        var mount = Assert.Single(release.Annotations.OfType<ContainerMountAnnotation>(), m => m.Target.StartsWith("/helm-chart-source", StringComparison.Ordinal));
        Assert.Equal(("/helm-chart-source", app), (mount.Target, mount.Source));
        Assert.Equal(["/tmp/helm-chart/libraries/library", "/tmp/helm-chart"], release.LocalChart!.BuildPaths);
    }

    [Fact]
    public void AChartDirectoryResolvesAgainstTheAppHostDirectory()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var relative = Path.GetRelativePath(appBuilder.AppHostDirectory, _chartDirectory);
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", relative);

        Assert.Equal(_chartDirectory, Release(appBuilder).LocalChart?.Directory);
    }

    [Fact]
    public void AChartDirectoryRefusesAVersion()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var cluster = appBuilder.AddK3sCluster("k8s");

        var exception = Assert.Throws<ArgumentException>(() => cluster.AddHelmRelease("app", _chartDirectory, version: "1.0.0"));
        Assert.Equal("version", exception.ParamName);
    }

    [Fact]
    public void ARepositoryChartIsNotTreatedAsADirectory()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", _chartDirectory, repo: "https://charts.example.com");

        var release = Release(appBuilder);

        Assert.Null(release.LocalChart);
        Assert.DoesNotContain(release.Annotations.OfType<ContainerMountAnnotation>(), m => m.Target == "/helm-chart-source");
    }
}

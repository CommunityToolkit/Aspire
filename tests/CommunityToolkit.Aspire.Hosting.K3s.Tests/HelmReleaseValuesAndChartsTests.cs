using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class HelmReleaseValuesAndChartsTests : IDisposable
{
    private readonly string _chartDirectory = Directory.CreateTempSubdirectory("aspire-k3s-chart").FullName;

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

    public void Dispose() => Directory.Delete(_chartDirectory, recursive: true);

    private static HelmReleaseResource Release(IDistributedApplicationBuilder appBuilder) =>
        appBuilder.Resources.OfType<HelmReleaseResource>().Single();

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

        Assert.Equal("ASPIRE_HELM_VALUE_0", release.HelmValueVariables["auth.password"]);
        Assert.DoesNotContain("s3cr", script);
        Assert.Contains("_aspire_helm_value_0=$(printf '%s' \"$ASPIRE_HELM_VALUE_0\" | sed -e 's/[\\\\,{}]/\\\\&/g')", script);
        Assert.Contains("--set 'auth.password='\"$_aspire_helm_value_0\"", script);
    }

    [Fact]
    public async Task AParameterValueIsSetAsAnEnvironmentVariable()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var password = appBuilder.AddParameter("password", "s3cret", secret: true);
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("auth.password", password);

        var environment = new Dictionary<string, object>();
        var context = new EnvironmentCallbackContext(appBuilder.ExecutionContext, environment);
        foreach (var annotation in Release(appBuilder).Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context);

        var value = Assert.IsType<ReferenceExpression>(environment["ASPIRE_HELM_VALUE_0"]);
        Assert.Equal("s3cret", await value.GetValueAsync(CancellationToken.None));
    }

    [Fact]
    public void SettingAKeyAgainKeepsItsVariable()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var first = appBuilder.AddParameter("first", "1");
        var second = appBuilder.AddParameter("second", "2");
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("key", first)
            .WithHelmValue("key", second);

        var release = Release(appBuilder);

        Assert.Equal("ASPIRE_HELM_VALUE_0", Assert.Single(release.HelmValueVariables).Value);
    }

    [Fact]
    public void ALiteralValueReplacesAnEarlierExpressionForTheSameKey()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var parameter = appBuilder.AddParameter("p", "resolved");
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", "oci://registry/app")
            .WithHelmValue("key", parameter)
            .WithHelmValue("key", "literal");

        var release = Release(appBuilder);
        var script = K3sHelmBuilderExtensions.BuildHelmScript(release);

        Assert.Empty(release.HelmValueVariables);
        Assert.Contains("--set 'key=literal'", script);
        Assert.DoesNotContain("_aspire_helm_value_", script);
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

        Assert.Equal(_chartDirectory, release.LocalChartDirectory);
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
        Assert.Contains("helm repo add --force-update \"aspire-k3s-dependency-$_dependency_repo\" \"$_repo\"", script);
        Assert.Contains("helm dependency build /tmp/helm-chart", script);
        Assert.Contains("helm upgrade --install 'app' '/tmp/helm-chart'", script);
        Assert.DoesNotContain("--version", script);
    }

    [Fact]
    public void AChartDirectoryResolvesAgainstTheAppHostDirectory()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        var relative = Path.GetRelativePath(appBuilder.AppHostDirectory, _chartDirectory);
        appBuilder.AddK3sCluster("k8s").AddHelmRelease("app", relative);

        Assert.Equal(_chartDirectory, Release(appBuilder).LocalChartDirectory);
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

        Assert.Null(release.LocalChartDirectory);
        Assert.DoesNotContain(release.Annotations.OfType<ContainerMountAnnotation>(), m => m.Target == "/helm-chart-source");
    }
}

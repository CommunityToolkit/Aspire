using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.DuckDB;

public class AddDuckDBTests
{
    [Fact]
    public void DistributedApplicationBuilderCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddDuckDB(null!));
    }

    [Fact]
    public void ResourceNameCannotBeOmitted()
    {
        string name = "";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddDuckDB(name));

        name = " ";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddDuckDB(name));

        name = null!;
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddDuckDB(name));
    }

    [Fact]
    public void EachResourceHasUniqueFile()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb1 = builder.AddDuckDB("duckdb");
        var duckdb2 = builder.AddDuckDB("duckdb2");
        Assert.NotEqual(duckdb1.Resource.DatabaseFileName, duckdb2.Resource.DatabaseFileName);
    }

    [Fact]
    public void ResourceIsRunningState()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb");

        Assert.True(duckdb.Resource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Running, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public void ResourceIncludedInManifestByDefault()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb");

        Assert.False(duckdb.Resource.TryGetAnnotationsOfType<ManifestPublishingCallbackAnnotation>(out var annotations));
    }

    [Fact]
    public void ResourceUsesTempPathWhenNoPathProvided()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb");

        Assert.Equal(Path.GetTempPath(), duckdb.Resource.DatabasePath);
    }

    [Fact]
    public void ResourceUsesRandomFileNameWhenNoFileNameProvided()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb");

        Assert.NotNull(duckdb.Resource.DatabaseFileName);
    }

    [Fact]
    public void ResourceUsesProvidedPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb", "/path/to/db");

        Assert.Equal("/path/to/db", duckdb.Resource.DatabasePath);
    }

    [Fact]
    public void ResourceUsesProvidedFileName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb", null, "mydb.duckdb");

        Assert.Equal("mydb.duckdb", duckdb.Resource.DatabaseFileName);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("/path/to/db", "mydb.duckdb")]
    public async Task ResourceUsesProvidedPathAndFileName(string? path, string? fileName)
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb", path, fileName);

        var connectionString = await duckdb.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        Assert.Equal($"DataSource={duckdb.Resource.DatabaseFilePath}", connectionString);
    }

    [Fact]
    public async Task WithReadOnlyAppendsAccessMode()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb")
            .WithReadOnly();

        var connectionString = await duckdb.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        Assert.Contains("Access Mode=ReadOnly", connectionString);
    }

    [Fact]
    public void ResourceFileNameHasDuckDBExtension()
    {
        var builder = DistributedApplication.CreateBuilder();
        var duckdb = builder.AddDuckDB("duckdb");

        Assert.EndsWith(".duckdb", duckdb.Resource.DatabaseFileName);
    }
}

using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Sqlite;
#pragma warning disable CTASPIRE002
public class AddSqliteTests
{
    [Fact]
    public void DistributedApplicationBuilderCannotBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddSqlite(null!));
    }

    [Fact]
    public void ResourceNameCannotBeOmitted()
    {
        string name = "";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddSqlite(name));

        name = " ";
        Assert.Throws<ArgumentException>(() => DistributedApplication.CreateBuilder().AddSqlite(name));

        name = null!;
        Assert.Throws<ArgumentNullException>(() => DistributedApplication.CreateBuilder().AddSqlite(name));
    }

    [Fact]
    public void EachResourceHasUniqueFile()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite1 = builder.AddSqlite("sqlite");
        var sqlite2 = builder.AddSqlite("sqlite2");
        Assert.NotEqual(sqlite1.Resource.DatabaseFileName, sqlite2.Resource.DatabaseFileName);
    }

    [Fact]
    public void ResourceIsRunningState()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite");

        Assert.True(sqlite.Resource.TryGetAnnotationsOfType<ResourceSnapshotAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Equal(KnownResourceStates.Running, annotation.InitialSnapshot.State?.Text);
    }

    [Fact]
    public void ResourceExcludedFromManifestByDefault()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite");

        Assert.True(sqlite.Resource.TryGetAnnotationsOfType<ManifestPublishingCallbackAnnotation>(out var annotations));
        var annotation = Assert.Single(annotations);

        Assert.Null(annotation.Callback);
    }

    [Fact]
    public void ResourceUsesTempPathWhenNoPathProvided()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite");

        Assert.Equal(Path.GetTempPath(), sqlite.Resource.DatabasePath);
    }

    [Fact]
    public void ResourceUsesRandomFileNameWhenNoFileNameProvided()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite");

        Assert.NotNull(sqlite.Resource.DatabaseFileName);
    }

    [Fact]
    public void ResourceUsesProvidedPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite", "/path/to/db");

        Assert.Equal("/path/to/db", sqlite.Resource.DatabasePath);
    }

    [Fact]
    public void ResourceUsesProvidedFileName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite", null, "mydb.db");

        Assert.Equal("mydb.db", sqlite.Resource.DatabaseFileName);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("/path/to/db", "mydb.db")]
    public async Task ResourceUsesProvidedPathAndFileName(string? path, string? fileName)
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite", path, fileName);

        var connectionString = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        Assert.Equal($"Data Source={sqlite.Resource.DatabaseFilePath};Cache=Shared;Mode=ReadWriteCreate;Extensions=[]", connectionString);
    }

    [Fact]
    public async Task SqliteWebResourceConfigured()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite")
            .WithSqliteWeb();

        var sqliteWeb = Assert.Single(builder.Resources.OfType<SqliteWebResource>());

        Assert.Equal($"{sqlite.Resource.Name}-sqliteweb", sqliteWeb.Name);
        Assert.Equal("http", sqliteWeb.PrimaryEndpoint.EndpointName);
        Assert.Equal(8080, sqliteWeb.PrimaryEndpoint.TargetPort);

        Assert.True(sqliteWeb.TryGetAnnotationsOfType<ContainerImageAnnotation>(out var imageAnnotations));
        var imageAnnotation = Assert.Single(imageAnnotations);
        Assert.Equal(SqliteContainerImageTags.SqliteWebImage, imageAnnotation.Image);
        Assert.Equal(SqliteContainerImageTags.SqliteWebTag, imageAnnotation.Tag);
        Assert.Equal(SqliteContainerImageTags.SqliteWebRegistry, imageAnnotation.Registry);

        var env = await sqliteWeb.GetEnvironmentVariableValuesAsync();
        var envVar = Assert.Single(env);
        Assert.Equal("SQLITE_DATABASE", envVar.Key);
        Assert.Equal(sqlite.Resource.DatabaseFileName, envVar.Value);

        Assert.True(sqliteWeb.TryGetAnnotationsOfType<ContainerMountAnnotation>(out var bindMountAnnotations));
        var bindMountAnnotation = Assert.Single(bindMountAnnotations);
        Assert.Equal(sqlite.Resource.DatabasePath, bindMountAnnotation.Source);
        Assert.Equal("/data", bindMountAnnotation.Target);

        var relationshipAnnotations = sqliteWeb.Annotations.OfType<ResourceRelationshipAnnotation>();

        var waitForAnnotation = relationshipAnnotations.FirstOrDefault(a => a.Type == "WaitFor");
        var parentAnnotation = relationshipAnnotations.FirstOrDefault(a => a.Type == "Parent");

        Assert.NotNull(waitForAnnotation);
        Assert.NotNull(parentAnnotation);
        Assert.Equal("sqlite", waitForAnnotation.Resource.Name);
        Assert.Equal("sqlite", parentAnnotation.Resource.Name);
    }

    [Fact]
    public void ResourceWithExtensionFromNuGet()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite")
            .WithNuGetExtension("FTS5");

        Assert.Single(sqlite.Resource.Extensions, static e => e.Extension == "FTS5" && e.PackageName == "FTS5" && e.IsNuGetPackage && e.ExtensionFolder is null);
    }

    [Fact]
    public void ResourceWithExtensionFromLocal()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite")
            .WithLocalExtension("FTS5", "/path/to/extension");

        Assert.Single(sqlite.Resource.Extensions, static e => e.Extension == "FTS5" && e.PackageName is null && !e.IsNuGetPackage && e.ExtensionFolder == "/path/to/extension");
    }

    [Fact]
    public async Task ConnectionStringContainsExtensions()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sqlite = builder.AddSqlite("sqlite")
            .WithNuGetExtension("FTS5")
            .WithNuGetExtension("mod_spatialite");

        var connectionString = await sqlite.Resource.ConnectionStringExpression.GetValueAsync(CancellationToken.None);

        Assert.Contains("FTS5", connectionString);
        Assert.Contains("mod_spatialite", connectionString);
    }
}

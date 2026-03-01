using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Neon.Tests;

public class NeonConnectionStringResolutionTests : IDisposable
{
    private readonly string _tempDir;

    public NeonConnectionStringResolutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"neon-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateEnvFile(string name, string connectionUri)
    {
        string filePath = Path.Combine(_tempDir, $"{name}.env");
        File.WriteAllText(filePath, $"NEON_CONNECTION_URI={connectionUri}\n");
        return filePath;
    }

    [Fact]
    public void AddNeonConnectionStrings_WithEnvFileVar_ResolvesConnectionString()
    {
        string envFile = CreateEnvFile("appdb", "postgresql://user:pass@host/db");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:appdb"] = envFile,
        });

        builder.AddNeonConnectionStrings();

        string? connectionString = builder.Configuration.GetConnectionString("appdb");
        Assert.Equal("postgresql://user:pass@host/db", connectionString);
    }

    [Fact]
    public void AddNeonConnectionStrings_WithMultipleEnvFileVars_ResolvesAll()
    {
        string envFile1 = CreateEnvFile("db1", "postgresql://user:pass@host/db1");
        string envFile2 = CreateEnvFile("db2", "postgresql://user:pass@host/db2");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:db1"] = envFile1,
            [$"NEON_ENV_FILE:db2"] = envFile2,
        });

        builder.AddNeonConnectionStrings();

        Assert.Equal("postgresql://user:pass@host/db1", builder.Configuration.GetConnectionString("db1"));
        Assert.Equal("postgresql://user:pass@host/db2", builder.Configuration.GetConnectionString("db2"));
    }

    [Fact]
    public void AddNeonConnectionStrings_WithOutputDir_ScansAndResolvesConnectionStrings()
    {
        CreateEnvFile("orders", "postgresql://user:pass@host/orders");
        CreateEnvFile("users", "postgresql://user:pass@host/users");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NEON_OUTPUT_DIR"] = _tempDir,
        });

        builder.AddNeonConnectionStrings();

        Assert.Equal("postgresql://user:pass@host/orders", builder.Configuration.GetConnectionString("orders"));
        Assert.Equal("postgresql://user:pass@host/users", builder.Configuration.GetConnectionString("users"));
    }

    [Fact]
    public void AddNeonConnectionStrings_DoesNotOverwriteExistingConnectionString()
    {
        string envFile = CreateEnvFile("appdb", "postgresql://new:new@host/db");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:appdb"] = "Host=existing;Database=db",
            [$"NEON_ENV_FILE:appdb"] = envFile,
        });

        builder.AddNeonConnectionStrings();

        Assert.Equal("Host=existing;Database=db", builder.Configuration.GetConnectionString("appdb"));
    }

    [Fact]
    public void AddNeonConnectionStrings_OutputDir_DoesNotOverwriteExistingConnectionString()
    {
        CreateEnvFile("appdb", "postgresql://new:new@host/db");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:appdb"] = "Host=existing;Database=db",
            ["NEON_OUTPUT_DIR"] = _tempDir,
        });

        builder.AddNeonConnectionStrings();

        Assert.Equal("Host=existing;Database=db", builder.Configuration.GetConnectionString("appdb"));
    }

    [Fact]
    public void AddNeonConnectionStrings_EnvFileVarTakesPrecedenceOverOutputDir()
    {
        // The NEON_ENV_FILE__{name} path runs first. Once it sets a connection
        // string, the NEON_OUTPUT_DIR scan should skip it.
        string specificDir = Path.Combine(_tempDir, "specific");
        Directory.CreateDirectory(specificDir);
        string specificFile = Path.Combine(specificDir, "appdb.env");
        File.WriteAllText(specificFile, "NEON_CONNECTION_URI=postgresql://specific:pass@host/db\n");

        string scanDir = Path.Combine(_tempDir, "scan");
        Directory.CreateDirectory(scanDir);
        File.WriteAllText(Path.Combine(scanDir, "appdb.env"), "NEON_CONNECTION_URI=postgresql://directory:pass@host/db\n");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:appdb"] = specificFile,
            ["NEON_OUTPUT_DIR"] = scanDir,
        });

        builder.AddNeonConnectionStrings();

        Assert.Equal("postgresql://specific:pass@host/db", builder.Configuration.GetConnectionString("appdb"));
    }

    [Fact]
    public void AddNeonConnectionStrings_SkipsMissingEnvFile()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:appdb"] = Path.Combine(_tempDir, "nonexistent.env"),
        });

        builder.AddNeonConnectionStrings();

        Assert.Null(builder.Configuration.GetConnectionString("appdb"));
    }

    [Fact]
    public void AddNeonConnectionStrings_SkipsEnvFileWithoutConnectionUri()
    {
        string filePath = Path.Combine(_tempDir, "appdb.env");
        File.WriteAllText(filePath, "SOME_OTHER_VAR=value\n");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:appdb"] = filePath,
        });

        builder.AddNeonConnectionStrings();

        Assert.Null(builder.Configuration.GetConnectionString("appdb"));
    }

    [Fact]
    public void AddNeonConnectionStrings_SkipsMissingOutputDir()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NEON_OUTPUT_DIR"] = Path.Combine(_tempDir, "nonexistent"),
        });

        builder.AddNeonConnectionStrings();

        // Should not throw, just skip.
    }

    [Fact]
    public void AddNeonConnectionStrings_ThrowsOnNullBuilder()
    {
        IHostApplicationBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddNeonConnectionStrings());
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddNeonConnectionStrings_ReturnsBuilderForFluentChaining()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);

        IHostApplicationBuilder result = builder.AddNeonConnectionStrings();

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddNeonConnectionStrings_HandlesSingleQuotedConnectionUri()
    {
        string filePath = Path.Combine(_tempDir, "appdb.env");
        File.WriteAllText(filePath, "NEON_CONNECTION_URI='postgresql://user:pass@host/db?sslmode=require'\n");

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:appdb"] = filePath,
        });

        builder.AddNeonConnectionStrings();

        string? connectionString = builder.Configuration.GetConnectionString("appdb");
        Assert.Equal("'postgresql://user:pass@host/db?sslmode=require'", connectionString);
    }

    [Fact]
    public void AddNeonClient_ResolvesConnectionFromEnvFile()
    {
        const string connString = "Host=neon-host.example.com;Port=5432;Database=db;Username=user;Password=pass";
        string envFile = CreateEnvFile("appdb", connString);

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:appdb"] = envFile,
        });

        builder.AddNeonClient("appdb");

        using var host = builder.Build();
        var dataSource = host.Services.GetService<Npgsql.NpgsqlDataSource>();
        Assert.NotNull(dataSource);
    }

    [Fact]
    public void AddNeonClient_WithOutputDir_ResolvesConnection()
    {
        const string connString = "Host=neon-host.example.com;Port=5432;Database=db;Username=user;Password=pass";
        CreateEnvFile("appdb", connString);

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NEON_OUTPUT_DIR"] = _tempDir,
        });

        builder.AddNeonClient("appdb");

        using var host = builder.Build();
        var dataSource = host.Services.GetService<Npgsql.NpgsqlDataSource>();
        Assert.NotNull(dataSource);
    }

    [Fact]
    public void AddKeyedNeonClient_ResolvesConnectionFromEnvFile()
    {
        const string connString = "Host=neon-host.example.com;Port=5432;Database=db;Username=user;Password=pass";
        string envFile = CreateEnvFile("appdb", connString);

        var builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"NEON_ENV_FILE:appdb"] = envFile,
        });

        var exception = Record.Exception(() => builder.AddKeyedNeonClient("appdb"));
        Assert.Null(exception);
    }
}

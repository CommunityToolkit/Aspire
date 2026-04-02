using Microsoft.EntityFrameworkCore;
using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.PostgreSQL;
// using AppAny.Quartz.EntityFrameworkCore.Migrations.SqlServer;
// using AppAny.Quartz.EntityFrameworkCore.Migrations.MySql;
// using AppAny.Quartz.EntityFrameworkCore.Migrations.SQLite;

namespace QuartzSample.ApiService.Data;

/// <summary>
/// DbContext for Quartz.NET tables with automatic migrations
/// Supports: PostgreSQL, SQL Server, MySQL, SQLite
/// Supports custom schemas (e.g., "quartz" schema in PostgreSQL)
/// </summary>
public class QuartzDbContext : DbContext
{
    private readonly IConfiguration _configuration;

    public QuartzDbContext(DbContextOptions<QuartzDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Get schema from configuration (optional)
        var schema = _configuration.GetValue<string>("Quartz:Schema");

        // ✅ PostgreSQL (Active)
        modelBuilder.AddQuartz(builder => builder.UsePostgreSql(schema: schema));

        // 💡 SQL Server (Commented - uncomment to use)
        // modelBuilder.AddQuartz(builder => builder.UseSqlServer(schema: schema));

        // 💡 MySQL (Commented - uncomment to use)
        // modelBuilder.AddQuartz(builder => builder.UseMySql(schema: schema));

        // 💡 SQLite (Commented - uncomment to use)
        // modelBuilder.AddQuartz(builder => builder.UseSQLite());
    }
}
